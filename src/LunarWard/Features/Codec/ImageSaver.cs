using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using SkiaSharp;

namespace LunarWard.Codec;

/// <summary>
/// 图像保存。对应上游 <c>Starward/Features/Codec/ImageSaver.cs</c>。
/// </summary>
/// <remarks>
/// 平台替换（逐条登记于 <c>docs/09-LINUX-DIVERGENCES.md</c>）：
/// <list type="bullet">
/// <item>PNG：Win2D <c>BitmapEncoder</c> + <c>PngReader</c> 重写色彩块 →
/// 自写 <see cref="PngEncoder"/>（IHDR + cICP/sRGB/cHRM/iCCP + iTXt + IDAT + IEND），
/// 写出规则与上游一致，并支持 16bit（Skia 不支持 Rgba16161616 编码）。</item>
/// <item>AVIF：<c>Starward.Codec.avifEncoderLite</c> → 外部 <c>avifenc</c>
/// （<c>--cicp</c> 传 BT.2020/SMPTE2084/BT.2020-NCL，自定义原色写 ICC 传 <c>--icc</c>）。</item>
/// <item>JXL：<c>JxlEncoder</c> → 外部 <c>cjxl</c>（distance 0 走 <c>--lossless</c>；
/// HDR10 的 CICP 元数据 libjxl 0.12 不写，已登记待办）。</item>
/// <item>UltraHDR：<c>UhdrEncoder</c>（Starward.Codec，ISO 21496-1）→
/// 基图 JPEG + 增益图 JPEG + XMP <c>hdrgm</c> + MPF 索引；色调映射/白点调整
/// （上游 <c>HdrToneMapEffect</c>/<c>WhiteLevelAdjustmentEffect</c>）用 CPU 实现。</item>
/// </list>
/// </remarks>
internal static class ImageSaver
{
    /// <summary>
    /// 建议并行线程数（与上游一致：核数 - 保留核）。
    /// </summary>
    public static int GetSuggestedThreads()
    {
        int processorCount = Environment.ProcessorCount;
        return processorCount switch
        {
            >= 16 => processorCount - 4,
            >= 8 => processorCount - 2,
            _ => processorCount,
        };
    }


    /// <summary>
    /// 保存为 PNG。
    /// </summary>
    public static async Task SaveAsPngAsync(SKBitmap bitmap, Stream stream, ICC.ColorPrimaries colorPrimaries, byte[]? xmpData = null, bool writeColorProfile = true)
    {
        await Task.Run(() =>
        {
            if (PixelBitmap.IsFloat(bitmap.ColorType))
            {
                // 与上游一致：scRGB 浮点先转 HDR10（BT.2020 + PQ + 16bit），色彩原色随之变为 BT.2020
                using SKBitmap hdr = Hdr10Transfer.ScRgbToHdr10(bitmap);
                PngEncoder.Write(stream, hdr, ICC.ColorPrimaries.BT2020, null, xmpData, writeColorProfile);
            }
            else
            {
                PngEncoder.Write(stream, bitmap, colorPrimaries, GetPngIccData(colorPrimaries), xmpData, writeColorProfile);
            }
        });
    }


    /// <summary>
    /// 从原始像素字节保存为 PNG。
    /// </summary>
    public static async Task SaveAsPngAsync(Stream stream, uint width, uint height, DirectXPixelFormat pixelFormat, byte[] pixelBytes, ICC.ColorPrimaries colorPrimaries, byte[]? xmpData = null, bool writeColorProfile = true)
    {
        using var bitmap = PixelBitmap.FromBytes(pixelBytes, (int)width, (int)height, pixelFormat);
        await SaveAsPngAsync(bitmap, stream, colorPrimaries, xmpData, writeColorProfile);
    }


    private static byte[]? GetPngIccData(ICC.ColorPrimaries colorPrimaries)
    {
        // 已知 CICP 原色用 cICP/sRGB 块即可；自定义原色写生成的 ICC（与上游写 iCCP 一致）
        return colorPrimaries.IsValid && colorPrimaries.Id == 0 ? ICC.ICCHelper.CreateIccData(colorPrimaries) : null;
    }


    /// <summary>
    /// 保存为 AVIF。
    /// </summary>
    /// <param name="quality">质量 0..100（与上游一样 clamp）。</param>
    public static async Task SaveAsAvifAsync(SKBitmap bitmap, Stream stream, ICC.ColorPrimaries colorPrimaries, int quality, byte[]? xmpData = null, bool writeColorProfile = true)
    {
        quality = Math.Clamp(quality, 0, 100);
        bool hdr = colorPrimaries.Id == 9;
        int depth = bitmap.ColorType == SKColorType.Rgba16161616 || PixelBitmap.IsFloat(bitmap.ColorType) ? 12 : 8;

        string pngPath = Path.Combine(CodecEnvironment.TempDirectory, $"{Guid.NewGuid():N}.png");
        string avifPath = Path.Combine(CodecEnvironment.TempDirectory, $"{Guid.NewGuid():N}.avif");
        string? iccPath = null;
        try
        {
            using (var png = File.Create(pngPath))
            {
                using SKBitmap source = PixelBitmap.IsFloat(bitmap.ColorType) ? Hdr10Transfer.ScRgbToHdr10(bitmap) : bitmap.Copy();
                PngEncoder.Write(png, source, colorPrimaries, GetPngIccData(colorPrimaries), null, writeColorProfile);
            }

            var arguments = new List<string>
            {
                pngPath,
                avifPath,
                "--yuv", "444",
                "--qcolor", quality.ToString(CultureInfo.InvariantCulture),
                "--depth", (depth == 8 ? 8 : 12).ToString(CultureInfo.InvariantCulture),
            };
            if (writeColorProfile)
            {
                if (colorPrimaries.Id == 9)
                {
                    arguments.AddRange(["--cicp", "9/16/9"]);
                }
                else if (colorPrimaries.Id == 1)
                {
                    arguments.AddRange(["--cicp", "1/13/6"]);
                }
                else if (colorPrimaries.Id == 12)
                {
                    arguments.AddRange(["--cicp", "12/13/0"]);
                }
                else if (colorPrimaries.IsValid)
                {
                    iccPath = Path.Combine(CodecEnvironment.TempDirectory, $"{Guid.NewGuid():N}.icc");
                    await File.WriteAllBytesAsync(iccPath, ICC.ICCHelper.CreateIccData(colorPrimaries));
                    arguments.AddRange(["--icc", iccPath]);
                }
            }

            var result = await ProcessRunner.RunAsync("avifenc", arguments, CancellationToken.None);
            if (result.ExitCode != 0 || !File.Exists(avifPath))
            {
                throw new InvalidOperationException($"avifenc failed ({result.ExitCode}): {result.StandardError.Trim()}");
            }
            await using var file = File.OpenRead(avifPath);
            await file.CopyToAsync(stream);
        }
        finally
        {
            TryDelete(pngPath);
            TryDelete(avifPath);
            if (iccPath is not null)
            {
                TryDelete(iccPath);
            }
        }
    }


    /// <summary>
    /// 保存为 JPEG XL。
    /// </summary>
    /// <param name="distance">距离 0..25（0 = 无损，与上游 clamp 一致）。</param>
    public static async Task SaveAsJxlAsync(SKBitmap bitmap, Stream stream, ICC.ColorPrimaries colorPrimaries, float distance, byte[]? xmpData = null, bool writeColorProfile = true)
    {
        distance = Math.Clamp(distance, 0, 25);
        string pngPath = Path.Combine(CodecEnvironment.TempDirectory, $"{Guid.NewGuid():N}.png");
        string jxlPath = Path.Combine(CodecEnvironment.TempDirectory, $"{Guid.NewGuid():N}.jxl");
        try
        {
            using (var png = File.Create(pngPath))
            {
                using SKBitmap source = PixelBitmap.IsFloat(bitmap.ColorType) ? Hdr10Transfer.ScRgbToHdr10(bitmap) : bitmap.Copy();
                PngEncoder.Write(png, source, colorPrimaries, GetPngIccData(colorPrimaries), null, writeColorProfile);
            }

            var arguments = new List<string> { pngPath, jxlPath };
            if (distance == 0)
            {
                arguments.Add("--lossless");
            }
            else
            {
                arguments.AddRange(["-d", distance.ToString("0.###", CultureInfo.InvariantCulture)]);
            }
            if (colorPrimaries.Id == 9)
            {
                // libjxl 0.12 的 cjxl 不接受 CICP 参数；HDR10 的 PQ/BT.2020 标注待 libjxl 支持（docs/09）。
                arguments.AddRange(["-e", "7"]);
            }

            var result = await ProcessRunner.RunAsync("cjxl", arguments, CancellationToken.None);
            if (result.ExitCode != 0 || !File.Exists(jxlPath))
            {
                throw new InvalidOperationException($"cjxl failed ({result.ExitCode}): {result.StandardError.Trim()}");
            }
            await using var file = File.OpenRead(jxlPath);
            await file.CopyToAsync(stream);
        }
        finally
        {
            TryDelete(pngPath);
            TryDelete(jxlPath);
        }
    }


    /// <summary>
    /// 保存为 UltraHDR JPEG（基图 + 增益图 + XMP <c>hdrgm</c> + MPF）。
    /// </summary>
    /// <remarks>
    /// 平台替换（<c>docs/09-LINUX-DIVERGENCES.md</c>）：上游用 <c>UhdrEncoder</c> 写
    /// ISO 21496-1 定义的 GContainer，本仓库按「基图 JPEG + MPF 索引的增益图 JPEG +
    /// XMP hdrgm 元数据」写出（Google UltraHDR 读取路径），未实现 GContainer box 形式。
    /// 色调映射：上游 <c>HdrToneMapEffect(DisplayMode=Hdr, InputMaxLuminance=maxCLL,
    /// OutputMaxLuminance=sdrWhiteLevel)</c> + <c>WhiteLevelAdjustmentEffect(InputWhiteLevel=80)</c>
    /// 在 CPU 上等价为 <c>sdr = clamp(hdr * 80 / sdrWhiteLevel, 0, 1)</c> 后接 sRGB OETF。
    /// </remarks>
    public static async Task SaveAsUhdrAsync(SKBitmap canvasImage, Stream stream, float maxCLL, float sdrWhiteLevel)
    {
        bool isHdr = PixelBitmap.IsFloat(canvasImage.ColorType) || canvasImage.ColorType == SKColorType.Rgba16161616;
        using SKBitmap hdrFloat = isHdr ? ToFloatBitmap(canvasImage) : PixelBitmap.Create(canvasImage.Width, canvasImage.Height, SKColorType.RgbaF32);
        var hdrPixels = PixelBitmap.ToFloatPixels(hdrFloat);
        int count = canvasImage.Width * canvasImage.Height;

        // SDR 参考（tone map + 白点调整后的线性值），用于增益公式
        float[] sdrLinear = new float[count * 4];
        float scale = 80f / MathF.Max(sdrWhiteLevel, 1f);
        for (int i = 0; i < count * 4; i += 4)
        {
            sdrLinear[i + 0] = MathF.Max(hdrPixels[i + 0], 0) * scale;
            sdrLinear[i + 1] = MathF.Max(hdrPixels[i + 1], 0) * scale;
            sdrLinear[i + 2] = MathF.Max(hdrPixels[i + 2], 0) * scale;
            sdrLinear[i + 3] = 1;
        }

        using var sdrReference = PixelBitmap.FromFloatPixels(sdrLinear, canvasImage.Width, canvasImage.Height);
        using var gainBitmap = new UhdrPixelGainEffect
        {
            SdrSource = sdrReference,
            HdrSource = hdrFloat,
        }.Apply();

        float[] contentBoost = GetContentMinMaxBoost(PixelBitmap.GetBytes(gainBitmap));
        using var gainmapBitmap = new UhdrGainmapEffect
        {
            PixelGainSource = gainBitmap,
            MinContentBoost = new System.Numerics.Vector3(contentBoost[0], contentBoost[1], contentBoost[2]),
            MaxContentBoost = new System.Numerics.Vector3(contentBoost[3], contentBoost[4], contentBoost[5]),
        }.Apply();

        // 基图：tone map 后 sRGB OETF，写 8bit
        using var baseBitmap = PixelBitmap.Create(canvasImage.Width, canvasImage.Height, SKColorType.Rgba8888);
        {
            var basePixels = baseBitmap.GetPixelSpan();
            for (int i = 0; i < count; i++)
            {
                int index = i * 4;
                basePixels[index + 0] = (byte)Math.Clamp((int)MathF.Round(SrgbGammaEffect.Oetf(Math.Clamp(sdrLinear[index + 0], 0, 1)) * 255f), 0, 255);
                basePixels[index + 1] = (byte)Math.Clamp((int)MathF.Round(SrgbGammaEffect.Oetf(Math.Clamp(sdrLinear[index + 1], 0, 1)) * 255f), 0, 255);
                basePixels[index + 2] = (byte)Math.Clamp((int)MathF.Round(SrgbGammaEffect.Oetf(Math.Clamp(sdrLinear[index + 2], 0, 1)) * 255f), 0, 255);
                basePixels[index + 3] = 255;
            }
        }

        // 增益图：log 归一化后写 8bit 灰度（放在 RGB 三个通道，与上游编码器一致）
        using var gainmapImage = PixelBitmap.Create(canvasImage.Width, canvasImage.Height, SKColorType.Rgba8888);
        {
            var gainPixels = PixelBitmap.ToFloatPixels(gainmapBitmap);
            var dst = gainmapImage.GetPixelSpan();
            for (int i = 0; i < count; i++)
            {
                int index = i * 4;
                dst[index + 0] = (byte)Math.Clamp((int)MathF.Round(gainPixels[index + 0] * 255f), 0, 255);
                dst[index + 1] = (byte)Math.Clamp((int)MathF.Round(gainPixels[index + 1] * 255f), 0, 255);
                dst[index + 2] = (byte)Math.Clamp((int)MathF.Round(gainPixels[index + 2] * 255f), 0, 255);
                dst[index + 3] = 255;
            }
        }

        byte[] baseJpeg = EncodeJpeg(baseBitmap);
        byte[] gainJpeg = EncodeJpeg(gainmapImage);

        float hdrCapacityMax = MathF.Max(MathF.Max(contentBoost[3], contentBoost[4]), MathF.Max(contentBoost[5], 1f));
        byte[] xmp = BuildUhdrXmp(contentBoost, hdrCapacityMax);
        byte[] uhdrJpeg = WriteMpfWithXmp(baseJpeg, gainJpeg, xmp);

        await stream.WriteAsync(uhdrJpeg);
    }


    private static SKBitmap ToFloatBitmap(SKBitmap bitmap)
    {
        var pixels = PixelBitmap.ToFloatPixels(bitmap);
        return PixelBitmap.FromFloatPixels(pixels, bitmap.Width, bitmap.Height);
    }

    private static byte[] EncodeJpeg(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90)
            ?? throw new InvalidOperationException("SkiaSharp failed to encode JPEG.");
        return data.ToArray();
    }


    private static byte[] BuildUhdrXmp(float[] contentBoost, float hdrCapacityMax)
    {
        static string Array3(float a, float b, float c)
            => string.Create(CultureInfo.InvariantCulture, $"{a:R} {b:R} {c:R}");

        string xmp =
            "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n" +
            "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">" +
            "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">" +
            "<rdf:Description rdf:about=\"\" xmlns:hdrgm=\"http://ns.adobe.com/hdr-gain-map/1.0/\"" +
            " hdrgm:Version=\"1.0\"" +
            $" hdrgm:BaseRenditionIsHDR=\"False\"" +
            $" hdrgm:GainMapMin=\"{Array3(contentBoost[0], contentBoost[1], contentBoost[2])}\"" +
            $" hdrgm:GainMapMax=\"{Array3(contentBoost[3], contentBoost[4], contentBoost[5])}\"" +
            " hdrgm:Gamma=\"1\"" +
            " hdrgm:OffsetSdr=\"0.015625\"" +
            " hdrgm:OffsetHdr=\"0.015625\"" +
            " hdrgm:HDRCapacityMin=\"1\"" +
            string.Create(CultureInfo.InvariantCulture, $" hdrgm:HDRCapacityMax=\"{hdrCapacityMax}\"") +
            " hdrgm:UseBaseColorSpace=\"1\"/>" +
            "</rdf:RDF></x:xmpmeta>\n<?xpacket end=\"w\"?>";
        return Encoding.UTF8.GetBytes(xmp);
    }


    /// <summary>
    /// 在基图 JPEG 中插入 XMP(APP1) 与 MPF(APP2)，并把增益图作为第二张图追加到文件尾。
    /// </summary>
    /// <remarks>
    /// MPF（CIPA DC-007）布局：APP2 payload = "MPF\0" + MP Header("MM",0x002A,"0030")
    /// + MP Index IFD（1 个 0xB002 标签 + Next IFD=0）+ 2 个 MP Entry（16 字节/个）。
    /// 两个 MP Entry 的 Data Offset 为文件绝对偏移，priMary 为 0、增益图为基图最终长度。
    /// 已知差异（docs/09 待办）：MP Entry 的 Individual Image Attribute 暂填 0，
    /// 未与 libultrahdr 的位定义逐位核对，需用真实 UltraHDR 读取器回归。
    /// </remarks>
    private static byte[] WriteMpfWithXmp(byte[] baseJpeg, byte[] gainJpeg, byte[] xmpPayload)
    {
        // MPF APP2 payload 布局（CIPA DC-007，TIFF 结构）：
        //   0..3   "MPF\0"
        //   4..7   byte order "MM" + 0x002A
        //   8..11  MP Index IFD 偏移（相对 MP Header 起点 = payload 偏移 4，值 8）
        //   12..   IFD: 3 个 tag（0xB000 MPFVersion "0030" / 0xB001 NumberOfImages / 0xB002 MPEntry）
        //   ...    Next IFD = 0，之后是 2 个 16 字节 MP Entry
        const int MpfInsertOffset = 2;                       // 插在 SOI 之后
        const int MpfSegmentHeader = 4;                      // FF E2 + length
        const int PayloadStart = MpfInsertOffset + MpfSegmentHeader;
        const int IfdOffsetInPayload = 12;
        const int EntriesOffsetInPayload = IfdOffsetInPayload + 2 + (3 * 12) + 4; // 54
        const int MpfPayloadSize = EntriesOffsetInPayload + (2 * 16);              // 86

        byte[] xmpSegment = BuildAppSegment(0xE1, [.. "http://ns.adobe.com/xap/1.0/\0"u8, .. xmpPayload]);
        byte[] baseWithXmp = InsertSegment(baseJpeg, xmpSegment);

        int finalBaseLength = baseWithXmp.Length + MpfSegmentHeader + MpfPayloadSize;

        byte[] payload = new byte[MpfPayloadSize];
        "MPF\0"u8.CopyTo(payload);
        payload[4] = 0x4D;                                   // 'M'
        payload[5] = 0x4D;                                   // 'M'
        payload[6] = 0x00;                                   // 0x002A
        payload[7] = 0x2A;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8), 8); // MP Index IFD 偏移

        int p = IfdOffsetInPayload;
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(p), 3);
        p += 2;
        // 0xB000 MPFVersion = "0030"（type 7，count 4，值内联在 offset 字段）
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(p + 0), 0xB000);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(p + 2), 7);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(p + 4), 4);
        "0030"u8.CopyTo(payload.AsSpan(p + 8));
        p += 12;
        // 0xB001 NumberOfImages = 2（type 4 = LONG，count 1）
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(p + 0), 0xB001);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(p + 2), 4);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(p + 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(p + 8), 2);
        p += 12;
        // 0xB002 MPEntry：2 条 × 16 字节，偏移为文件的绝对偏移
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(p + 0), 0xB002);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(p + 2), 7);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(p + 4), 2 * 16);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(p + 8), (uint)(PayloadStart + EntriesOffsetInPayload));
        p += 12;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(p), 0);   // Next IFD

        // MP Entry 1：基图（Data Offset 0，Size = 基图最终长度）
        int entry = EntriesOffsetInPayload;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(entry + 4), (uint)finalBaseLength);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(entry + 8), 0);
        // MP Entry 2：增益图（追加在基图之后）
        entry += 16;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(entry + 4), (uint)gainJpeg.Length);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(entry + 8), (uint)finalBaseLength);

        byte[] mpfSegment = BuildAppSegment(0xE2, payload);
        byte[] baseWithMpf = InsertSegment(baseWithXmp, mpfSegment);

        byte[] result = new byte[baseWithMpf.Length + gainJpeg.Length];
        baseWithMpf.CopyTo(result, 0);
        gainJpeg.CopyTo(result, baseWithMpf.Length);
        return result;
    }


    private static byte[] InsertSegment(byte[] jpeg, byte[] segment)
    {
        // 插到 SOI 之后
        byte[] result = new byte[jpeg.Length + segment.Length];
        jpeg.AsSpan(0, 2).CopyTo(result);
        segment.CopyTo(result, 2);
        jpeg.AsSpan(2).CopyTo(result.AsSpan(2 + segment.Length));
        return result;
    }

    private static byte[] BuildAppSegment(byte marker, byte[] payload)
    {
        byte[] segment = new byte[4 + payload.Length];
        segment[0] = 0xFF;
        segment[1] = marker;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2, 2), (ushort)(payload.Length + 2));
        payload.CopyTo(segment, 4);
        return segment;
    }


    /// <summary>
    /// 计算增益图的逐通道最小/最大内容增益。对应上游 <c>GetContentMinMaxBoost</c>。
    /// </summary>
    /// <returns>[minR, minG, minB, maxR, maxG, maxB]</returns>
    public static float[] GetContentMinMaxBoost(byte[] pixelBytes)
    {
        const float PQ_MAX = 10000f / 203f;
        var pixels = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(pixelBytes);
        float minR = PQ_MAX, minG = PQ_MAX, minB = PQ_MAX;
        float maxR = 0, maxG = 0, maxB = 0;
        for (int i = 0; i + 3 < pixels.Length; i += 4)
        {
            float r = pixels[i + 0];
            float g = pixels[i + 1];
            float b = pixels[i + 2];
            if (r < minR)
            {
                minR = r;
            }
            if (g < minG)
            {
                minG = g;
            }
            if (b < minB)
            {
                minB = b;
            }
            if (r > maxR)
            {
                maxR = r;
            }
            if (g > maxG)
            {
                maxG = g;
            }
            if (b > maxB)
            {
                maxB = b;
            }
        }
        return [minR, minG, minB, maxR, maxG, maxB];
    }


    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
