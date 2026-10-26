using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace LunarWard.Codec;

/// <summary>
/// 图像加载。对应上游 <c>Starward/Features/Codec/ImageLoader.cs</c>。
/// </summary>
/// <remarks>
/// 平台替换（逐条登记于 <c>docs/09-LINUX-DIVERGENCES.md</c>）：
/// <list type="bullet">
/// <item>Win2D <c>CanvasBitmap.LoadAsync</c> → SkiaSharp <c>SKCodec</c>（8bit 常规格式）；
/// PNG 走自写 <see cref="PngDecoder"/>（Skia 不支持 16bit PNG 解码）。</item>
/// <item><c>Starward.Codec</c> 的 <c>avifDecoderLite</c>/<c>JxlDecoderLite</c>（源码不在仓库）
/// → 外部 <c>avifdec</c> / <c>djxl</c> 解码到 16bit PNG 再读；色彩信息自己解析
/// AVIF <c>colr</c>/<c>pixi</c> box 与 JXL <c>jxlinfo</c> 输出，不依赖 Skia 的 AVIF/JXL 支持
/// （实测 SkiaSharp 3.119.4 对两者 <c>SKCodec.Create</c> 返回 null）。</item>
/// <item>HDR10→scRGB 的 <c>HDR10ToScRGBEffect</c>（D2D 着色器）→
/// <see cref="HDR10ToScRGBEffect"/> 的 CPU 内核，PQ/矩阵常量一致。</item>
/// <item><c>Windows.Graphics.Imaging.BitmapDecoder</c> 取像素尺寸 → Skia/容器头解析。</item>
/// </list>
/// 公开方法签名（方法名、参数名、返回类型形状）与上游一致，仅像素容器类型改为 SKBitmap。
/// </remarks>
internal static class ImageLoader
{
    /// <summary>
    /// 加载图片。
    /// </summary>
    /// <param name="filePath">图片路径。</param>
    /// <param name="cancellation">取消令牌。</param>
    public static async Task<ImageInfo> LoadImageAsync(string filePath, CancellationToken cancellation = default)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        ImageInfo info = extension switch
        {
            ".avif" => await LoadAvifAsync(filePath, cancellation),
            ".jxl" => await LoadJxlAsync(filePath, cancellation),
            ".png" => await LoadPngAsync(filePath, cancellation),
            ".jpg" or ".jpeg" => await LoadJpegAsync(filePath, cancellation),
            _ => await LoadConventionalAsync(filePath, cancellation),
        };

        // 与上游一致：无法识别色彩原色时兜底 BT.709
        if (info.ColorPrimaries is null || !info.ColorPrimaries.IsValid)
        {
            info.ColorPrimaries = ICC.ColorPrimaries.BT709;
        }
        return info;
    }


    /// <summary>
    /// 只读图片像素尺寸（不完整解码）。对应上游
    /// <c>GetImagePixelSizeAsync</c>。
    /// </summary>
    public static async Task<(uint Width, uint Height)> GetImagePixelSizeAsync(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension is ".avif")
        {
            byte[] bytes = await File.ReadAllBytesAsync(filePath);
            var color = AvifContainer.TryRead(bytes);
            if (color is { Width: > 0, Height: > 0 })
            {
                return ((uint)color.Width, (uint)color.Height);
            }
        }
        else if (extension is ".jxl")
        {
            var info = await RunJxlinfoAsync(filePath, CancellationToken.None);
            if (info is not null)
            {
                return ((uint)info.Width, (uint)info.Height);
            }
        }
        else if (extension is ".png")
        {
            await using var stream = File.OpenRead(filePath);
            var png = PngDecoder.ReadImageInfo(stream);
            return ((uint)png.Width, (uint)png.Height);
        }

        using var fileStream = File.OpenRead(filePath);
        using var codec = SKCodec.Create(fileStream);
        if (codec is null)
        {
            // 最后兜底：外部 jxlinfo 也能报尺寸
            var info = await RunJxlinfoAsync(filePath, CancellationToken.None);
            if (info is not null)
            {
                return ((uint)info.Width, (uint)info.Height);
            }
            throw new InvalidOperationException($"Cannot read image size: {filePath}");
        }
        return ((uint)codec.Info.Width, (uint)codec.Info.Height);
    }


    private static async Task<ImageInfo> LoadPngAsync(string filePath, CancellationToken cancellation)
    {
        byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellation);
        var info = new ImageInfo();
        try
        {
            using var fs = new MemoryStream(bytes, writable: false);
            var reader = new PngReader(fs);
            PngChunk chunk;
            while ((chunk = reader.GetNextChunk()).Type != PngChunkType.IEND)
            {
                if (chunk.Type == PngChunkType.cICP)
                {
                    var cicp = chunk.GetcICPChunk();
                    info.ColorPrimaries = cicp.ColorPrimaries switch
                    {
                        1 => ICC.ColorPrimaries.BT709,
                        9 => ICC.ColorPrimaries.BT2020,
                        12 => ICC.ColorPrimaries.DisplayP3,
                        _ => new ICC.ColorPrimaries { Id = cicp.ColorPrimaries },
                    };
                    break;
                }
                else if (chunk.Type == PngChunkType.iCCP)
                {
                    byte[] icc = chunk.GetiCCPChunk(out _);
                    if (icc.Length > 0)
                    {
                        info.IccData = icc;
                        if (ICC.ICCHelper.GetColorPrimariesFromIccData(icc) is { } primaries)
                        {
                            info.ColorPrimaries = primaries;
                        }
                    }
                    break;
                }
                else if (chunk.Type == PngChunkType.sRGB)
                {
                    info.ColorPrimaries = ICC.ColorPrimaries.BT709;
                    break;
                }
                else if (chunk.Type == PngChunkType.cHRM)
                {
                    var chrm = chunk.GetcHRMChunk();
                    info.ColorPrimaries = new ICC.ColorPrimaries
                    {
                        Red = new Vector2(chrm.RedX, chrm.RedY),
                        Green = new Vector2(chrm.GreenX, chrm.GreenY),
                        Blue = new Vector2(chrm.BlueX, chrm.BlueY),
                        White = new Vector2(chrm.WhiteX, chrm.WhiteY),
                    };
                    break;
                }
            }
        }
        catch
        {
            // 与上游一致：色彩块解析失败不影响像素加载
        }

        var bitmap = PngDecoder.Decode(bytes);
        if (info.ColorPrimaries.Id == 9)
        {
            // HDR10 PNG：16bit 归一化 → scRGB 浮点
            using (bitmap)
            {
                info.CanvasBitmap = Hdr10Transfer.Hdr10ToScRgb(bitmap);
            }
            info.HDR = true;
            info.ColorPrimaries = ICC.ColorPrimaries.BT709;
        }
        else
        {
            info.CanvasBitmap = bitmap;
        }
        return info;
    }


    private static async Task<ImageInfo> LoadJpegAsync(string filePath, CancellationToken cancellation)
    {
        byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellation);
        var info = new ImageInfo();
        info.ColorPrimaries = ICC.ColorPrimaries.BT709;
        try
        {
            byte[]? icc = JpegMetadata.ReadIccProfile(bytes);
            if (icc is { Length: > 14 })
            {
                info.IccData = icc;
                if (ICC.ICCHelper.GetColorPrimariesFromIccData(icc) is { } primaries)
                {
                    info.ColorPrimaries = primaries;
                }
            }
        }
        catch
        {
            // UltraHDR JPEG 的元数据损坏时按普通 JPEG 处理（与上游 catch 后直接 LoadAsync 一致）
        }

        info.CanvasBitmap = DecodeWithSkia(bytes);
        return info;
    }


    private static async Task<ImageInfo> LoadConventionalAsync(string filePath, CancellationToken cancellation)
    {
        byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellation);
        var info = new ImageInfo
        {
            CanvasBitmap = DecodeWithSkia(bytes),
        };
        info.HDR = PixelBitmap.IsFloat(info.CanvasBitmap.ColorType);
        return info;
    }


    /// <summary>
    /// AVIF 加载。上游用 <c>avifDecoderLite</c>；本仓库用 <c>avifdec</c> 解到 16bit PNG。
    /// </summary>
    private static async Task<ImageInfo> LoadAvifAsync(string filePath, CancellationToken cancellation)
    {
        byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellation);
        var color = AvifContainer.TryRead(bytes) ?? new AvifContainer.ColorInfo(0, 2, 0, 8, null, 0, 0);

        var info = new ImageInfo();
        info.IccData = color.Icc;
        if (color.Icc is { Length: > 0 } && ICC.ICCHelper.GetColorPrimariesFromIccData(color.Icc) is { } iccPrimaries)
        {
            info.ColorPrimaries = iccPrimaries;
        }
        else
        {
            info.ColorPrimaries = color.Primaries switch
            {
                1 => ICC.ColorPrimaries.BT709,
                9 => ICC.ColorPrimaries.BT2020,
                12 => ICC.ColorPrimaries.DisplayP3,
                _ => new ICC.ColorPrimaries { Id = color.Primaries },
            };
        }

        bool hdr10 = color.Primaries == 9 && color.Transfer == 16 && color.Depth >= 12;
        int depth = color.Depth >= 12 ? 16 : 8;
        using var bitmap = await DecodeExternalAsync("avifdec", ["-d", depth.ToString(CultureInfo.InvariantCulture), filePath, "{out}"], cancellation);

        if (hdr10)
        {
            info.CanvasBitmap = Hdr10Transfer.Hdr10ToScRgb(bitmap);
            info.HDR = true;
            info.ColorPrimaries = ICC.ColorPrimaries.BT709;
        }
        else
        {
            info.CanvasBitmap = bitmap.Copy();
        }
        return info;
    }


    /// <summary>
    /// JXL 加载。上游用 <c>JxlDecoderLite</c>；本仓库用 <c>djxl</c> 解到 16bit PNG，
    /// 色彩编码读 <c>jxlinfo</c>，ICC 读解码后 PNG 的 iCCP 块。
    /// </summary>
    private static async Task<ImageInfo> LoadJxlAsync(string filePath, CancellationToken cancellation)
    {
        var header = await RunJxlinfoAsync(filePath, cancellation) ?? new JxlHeader(0, 0, 8, "sRGB", "sRGB", null);
        var info = new ImageInfo();

        int depth = header.Depth >= 12 ? 16 : 8;
        var pngBytes = await DecodeExternalPngAsync("djxl", [$"--bits_per_sample={depth}", filePath, "{out}"], cancellation);

        // 解码后的 PNG 保留了原文件的 ICC（libjxl 会写 iCCP），优先用它判定原色
        byte[]? icc = ReadPngIcc(pngBytes);
        if (icc is not null && ICC.ICCHelper.GetColorPrimariesFromIccData(icc) is { } iccPrimaries)
        {
            info.IccData = icc;
            info.ColorPrimaries = iccPrimaries;
        }
        else
        {
            info.IccData = icc;
            info.ColorPrimaries = header.Primaries switch
            {
                "2100" => ICC.ColorPrimaries.BT2020,
                "P3" or "P3 D65" => ICC.ColorPrimaries.DisplayP3,
                "sRGB" => ICC.ColorPrimaries.BT709,
                "Custom" or "unknown" => ICC.ColorPrimaries.BT709,
                _ => ICC.ColorPrimaries.BT709,
            };
        }

        bool hdr10 = header.Primaries == "2100" && header.Transfer == "PQ" && header.Depth >= 12;
        using var bitmap = PngDecoder.Decode(pngBytes);
        if (hdr10)
        {
            info.CanvasBitmap = Hdr10Transfer.Hdr10ToScRgb(bitmap);
            info.HDR = true;
            info.ColorPrimaries = ICC.ColorPrimaries.BT709;
        }
        else
        {
            info.CanvasBitmap = bitmap.Copy();
        }
        return info;
    }


    private static byte[]? ReadPngIcc(byte[] png)
    {
        try
        {
            using var stream = new MemoryStream(png, writable: false);
            var reader = new PngReader(stream);
            PngChunk chunk;
            while ((chunk = reader.GetNextChunk()).Type != PngChunkType.IEND)
            {
                if (chunk.Type == PngChunkType.iCCP)
                {
                    return chunk.GetiCCPChunk(out _);
                }
            }
        }
        catch
        {
        }
        return null;
    }


    private static SKBitmap DecodeWithSkia(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(stream)
            ?? throw new NotSupportedException("Unsupported image format on this platform (see docs/09-LINUX-DIVERGENCES.md).");
        var imageInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bitmap = PixelBitmap.Create(imageInfo.Width, imageInfo.Height, SKColorType.Rgba8888);
        var result = codec.GetPixels(imageInfo, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw new InvalidOperationException($"SkiaSharp failed to decode image: {result}");
        }
        return bitmap;
    }


    /// <summary>
    /// 调用外部解码器解到 16bit PNG 并解码为位图。
    /// </summary>
    private static async Task<SKBitmap> DecodeExternalAsync(string fileName, string[] arguments, CancellationToken cancellation)
    {
        byte[] png = await DecodeExternalPngAsync(fileName, arguments, cancellation);
        return PngDecoder.Decode(png);
    }

    /// <summary>
    /// 调用外部解码器（avifdec / djxl）解到 PNG，返回 PNG 字节。
    /// </summary>
    private static async Task<byte[]> DecodeExternalPngAsync(string fileName, string[] arguments, CancellationToken cancellation)
    {
        string outputPath = Path.Combine(CodecEnvironment.TempDirectory, $"{Guid.NewGuid():N}.png");
        try
        {
            string[] finalArgs = arguments.Select(a => a.Replace("{out}", outputPath, StringComparison.Ordinal)).ToArray();
            var result = await ProcessRunner.RunAsync(fileName, finalArgs, cancellation);
            if (result.ExitCode != 0 || !File.Exists(outputPath))
            {
                throw new InvalidOperationException(
                    $"{fileName} failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
            }
            return await File.ReadAllBytesAsync(outputPath, cancellation);
        }
        finally
        {
            try
            {
                File.Delete(outputPath);
            }
            catch
            {
            }
        }
    }


    internal sealed record JxlHeader(int Width, int Height, int Depth, string Primaries, string Transfer, string? ColorSpace);


    private static async Task<JxlHeader?> RunJxlinfoAsync(string filePath, CancellationToken cancellation)
    {
        ProcessResult result;
        try
        {
            result = await ProcessRunner.RunAsync("jxlinfo", [filePath], cancellation);
        }
        catch
        {
            return null;
        }
        if (result.ExitCode != 0)
        {
            return null;
        }
        return ParseJxlinfo(result.StandardOutput);
    }

    internal static JxlHeader? ParseJxlinfo(string output)
    {
        var size = Regex.Match(output, @"(\d+)x(\d+)");
        if (!size.Success)
        {
            return null;
        }
        int width = int.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture);
        int height = int.Parse(size.Groups[2].Value, CultureInfo.InvariantCulture);
        var depthMatch = Regex.Match(output, @"(\d+)-bit");
        int depth = depthMatch.Success ? int.Parse(depthMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 8;
        var primaries = Regex.Match(output, @"^Primaries:\s*(.+)$", RegexOptions.Multiline);
        var transfer = Regex.Match(output, @"^Transfer function:\s*(.+)$", RegexOptions.Multiline);
        var colorSpace = Regex.Match(output, @"^Color space:\s*(.+)$", RegexOptions.Multiline);
        return new JxlHeader(
            width,
            height,
            depth,
            primaries.Success ? primaries.Groups[1].Value.Trim() : string.Empty,
            transfer.Success ? transfer.Groups[1].Value.Trim() : string.Empty,
            colorSpace.Success ? colorSpace.Groups[1].Value.Trim() : null);
    }
}


/// <summary>
/// 编解码器运行环境（临时目录、外部工具定位）。
/// </summary>
internal static class CodecEnvironment
{
    private static readonly Lazy<string> TempDirectoryLazy = new(() =>
    {
        string path = Path.Combine(Path.GetTempPath(), "lunarward-codec");
        Directory.CreateDirectory(path);
        return path;
    });

    public static string TempDirectory => TempDirectoryLazy.Value;
}


/// <summary>
/// 外部进程执行（avifdec / avifenc / djxl / cjxl / jxlinfo / ffmpeg）。
/// </summary>
internal static class ProcessRunner
{
    internal sealed record Result(int ExitCode, string StandardOutput, string StandardError);

    public static async Task<Result> RunAsync(string fileName, IEnumerable<string> arguments, CancellationToken cancellation)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start '{fileName}'. Install it (see docs/09-LINUX-DIVERGENCES.md): {ex.Message}", ex);
        }
        var stdout = process.StandardOutput.ReadToEndAsync(cancellation);
        var stderr = process.StandardError.ReadToEndAsync(cancellation);
        await process.WaitForExitAsync(cancellation);
        return new Result(process.ExitCode, await stdout, await stderr);
    }
}


/// <summary>
/// AVIF（ISO-BMFF）色彩与尺寸信息解析。
/// </summary>
/// <remarks>
/// 平台替换（<c>docs/09-LINUX-DIVERGENCES.md</c>）：上游 <c>avifDecoderLite</c> 直接暴露
/// <c>avifImage</c> 的 colorPrimaries/transferCharacteristics/ICC/depth；本仓库解析
/// <c>colr</c>（nclx / prof）、<c>pixi</c>、<c>ispe</c> box 得到同样的信息。
/// </remarks>
internal static class AvifContainer
{
    internal sealed record ColorInfo(int Primaries, int Transfer, int Matrix, int Depth, byte[]? Icc, int Width, int Height);

    private static readonly HashSet<string> ContainerBoxes = ["meta", "iprp", "ipco", "moov", "trak", "mdia", "minf", "stbl", "dinf", "edts", "udta", "ipma"];

    public static ColorInfo? TryRead(byte[] file)
    {
        int primaries = 0, transfer = 2, matrix = 0, depth = 8, width = 0, height = 0;
        byte[]? icc = null;
        bool found = false;

        void Walk(ReadOnlySpan<byte> data, int start, int end, int depthLevel)
        {
            int offset = start;
            while (offset + 8 <= end)
            {
                long size = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
                string type = Encoding.ASCII.GetString(data.Slice(offset + 4, 4));
                int headerSize = 8;
                if (size == 1)
                {
                    if (offset + 16 > end)
                    {
                        return;
                    }
                    size = (long)BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset + 8, 8));
                    headerSize = 16;
                }
                else if (size == 0)
                {
                    size = end - offset;
                }
                if (size < headerSize || offset + size > end)
                {
                    return;
                }

                var payload = data.Slice(offset + headerSize, (int)size - headerSize);
                switch (type)
                {
                    case "colr":
                        if (payload.Length >= 4)
                        {
                            string colourType = Encoding.ASCII.GetString(payload[..4]);
                            if (colourType == "nclx" && payload.Length >= 11)
                            {
                                primaries = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(4, 2));
                                transfer = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(6, 2));
                                matrix = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(8, 2));
                                found = true;
                            }
                            else if (colourType == "prof")
                            {
                                icc = payload[4..].ToArray();
                                found = true;
                            }
                        }
                        break;
                    case "pixi":
                        if (payload.Length >= 5)
                        {
                            depth = payload[4];
                        }
                        break;
                    case "ispe":
                        if (payload.Length >= 12)
                        {
                            width = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(4, 4));
                            height = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(8, 4));
                        }
                        break;
                    default:
                        if (ContainerBoxes.Contains(type) && depthLevel < 8)
                        {
                            // meta 是 FullBox，子 box 从 +4 开始
                            int childStart = offset + headerSize + (type == "meta" ? 4 : 0);
                            Walk(data, childStart, offset + (int)size, depthLevel + 1);
                        }
                        break;
                }
                offset += (int)size;
            }
        }

        Walk(file, 0, file.Length, 0);
        if (!found && width == 0 && height == 0)
        {
            return null;
        }
        return new ColorInfo(primaries, transfer, matrix, depth, icc, width, height);
    }
}


/// <summary>
/// JPEG 元数据（APP2 ICC 段）读取，替代上游 <c>Starward.Codec.UhdrDecoder.GetIccData</c>。
/// </summary>
internal static class JpegMetadata
{
    /// <summary>
    /// 读取 ICC 配置文件（按 ICC_PROFILE 顺序拼接多个 APP2 段）。
    /// 返回的是剥离 14 字节 <c>ICC_PROFILE\0</c> 头之后的纯 profile。
    /// </summary>
    public static byte[]? ReadIccProfile(byte[] jpeg)
    {
        var segments = new SortedDictionary<int, byte[]>();
        int offset = 2; // SOI
        while (offset + 4 <= jpeg.Length)
        {
            if (jpeg[offset] != 0xFF)
            {
                break;
            }
            byte marker = jpeg[offset + 1];
            if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                offset += 2;
                continue;
            }
            int length = BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(offset + 2, 2));
            if (length < 2 || offset + 2 + length > jpeg.Length)
            {
                break;
            }
            if (marker == 0xE2 && length >= 16)
            {
                var payload = jpeg.AsSpan(offset + 4, length - 2);
                if (payload[..12].SequenceEqual("ICC_PROFILE\0"u8))
                {
                    int sequence = payload[12];
                    int count = payload[13];
                    if (count > 0)
                    {
                        segments[sequence] = payload[14..].ToArray();
                    }
                }
            }
            if (marker == 0xDA)
            {
                break; // 进入扫描数据
            }
            offset += 2 + length;
        }

        if (segments.Count == 0)
        {
            return null;
        }
        return segments.Values.SelectMany(x => x).ToArray();
    }
}
