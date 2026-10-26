using System.IO.Hashing;
using System.Security.Cryptography;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace LunarWard.Codec;

/// <summary>
/// 图片/视频缩略图。对应上游 <c>Starward/Features/Codec/ImageThumbnail.cs</c>。
/// </summary>
/// <remarks>
/// 平台替换（逐条登记于 <c>docs/09-LINUX-DIVERGENCES.md</c>）：
/// <list type="bullet">
/// <item>返回类型：WinRT <c>BitmapSource</c> → Avalonia <see cref="Bitmap"/>（上层只把结果赋给 Image.Source，用法不变）。</item>
/// <item>解码：<c>BitmapDecoder</c> + Fant 缩放 + <c>ColorManageToSRgb</c> → <see cref="ImageLoader"/>
/// 解码后由 Skia <c>SKBitmap.Resize</c> 缩放（AVIF/JXL 走外部解码器，见 ImageLoader）。</item>
/// <item>视频缩略图：<c>MediaPlayer</c> 首帧 / <c>StorageFile.GetThumbnailAsync</c> → 外部 <c>ffmpeg</c> 抽首帧。</item>
/// <item>缓存目录：<c>AppConfig.CacheFolder/thumb</c>（与上游同路径），缓存文件名规则不变。</item>
/// </list>
/// </remarks>
internal static class ImageThumbnail
{

    internal static readonly string CacheFolder = Path.Combine(AppConfig.CacheFolder, "thumb");


    /// <summary>
    /// 上游探测 WIC/Skia 是否带 AVIF 解码器；本仓库 SkiaSharp 不带 AVIF/JXL，改为探测外部解码器。
    /// </summary>
    public static readonly bool AvifDecoderSupported = IsExecutableAvailable("avifdec");

    /// <summary>
    /// 上游探测 JXL 解码器；本仓库探测外部解码器。
    /// </summary>
    public static readonly bool JxlDecoderSupported = IsExecutableAvailable("djxl");


    private static readonly SemaphoreSlim _semaphore = new(Math.Max(1, Environment.ProcessorCount / 2));


    /// <summary>
    /// 目标缩略图尺寸（保持长宽比，限制在 412×232 内）与上游一致。
    /// </summary>
    private static (int Width, int Height) GetThumbnailSize(int width, int height)
    {
        const int TargetWidth = 412;
        const int TargetHeight = 232;
        if (width * TargetHeight > height * TargetWidth)
        {
            return (TargetHeight * width / height, TargetHeight);
        }
        else
        {
            return (TargetWidth, TargetWidth * height / width);
        }
    }


    /// <summary>
    /// 缓存文件名：路径的 XxHash64(前 8 字节) + MD5(后 8 字节)，24 位大写十六进制。与上游算法一致。
    /// </summary>
    private static string GetCachedThumbnailName(string path)
    {
        byte[] hash = new byte[24];
        XxHash64.Hash(System.Text.Encoding.UTF8.GetBytes(path)).CopyTo(hash.AsSpan(0, 8));
        MD5.HashData(System.Text.Encoding.UTF8.GetBytes(path)).AsSpan(0, 8).CopyTo(hash.AsSpan(8, 8));
        return Convert.ToHexString(hash, 0, 16).ToUpperInvariant();
    }


    /// <summary>
    /// 获取图片缩略图；优先读缓存文件。
    /// </summary>
    /// <param name="path">图片路径。</param>
    /// <param name="png">缓存是否写成 png（webp 带 alpha 时始终写 png，与上游一致）。</param>
    public static async Task<Bitmap> GetImageThumbnailAsync(string path, bool png, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string cachedPath = Path.Combine(CacheFolder, GetCachedThumbnailName(path) + (png ? ".png" : ".jpg"));
            if (File.Exists(cachedPath))
            {
                return new Bitmap(cachedPath);
            }
            Directory.CreateDirectory(CacheFolder);

            // 与上游一致：等待一点时间，避免列表快速滚动时同时解码大量图片
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);

            (uint width, uint height) = await ImageLoader.GetImagePixelSizeAsync(path).ConfigureAwait(false);
            (int targetWidth, int targetHeight) = GetThumbnailSize((int)width, (int)height);

            using ImageInfo info = await ImageLoader.LoadImageAsync(path, cancellationToken).ConfigureAwait(false);
            using SKBitmap thumbnail = Resize(info.CanvasBitmap, targetWidth, targetHeight);

            bool writePng = png || HasAlpha(path);
            string outputPath = Path.Combine(CacheFolder, GetCachedThumbnailName(path) + (writePng ? ".png" : ".jpg"));
            using (var file = File.Create(outputPath))
            {
                if (writePng)
                {
                    PngEncoder.Write(file, thumbnail, info.ColorPrimaries, null, null, true);
                }
                else
                {
                    using var image = SKImage.FromBitmap(thumbnail);
                    using var data = image.Encode(SKEncodedImageFormat.Jpeg, 60)
                        ?? throw new InvalidOperationException("SkiaSharp failed to encode thumbnail JPEG.");
                    data.SaveTo(file);
                }
            }
            return new Bitmap(outputPath);
        }
        finally
        {
            _semaphore.Release();
        }
    }


    /// <summary>
    /// 获取视频缩略图（首帧）。上游用 MediaPlayer / StorageFile 缩略图，本仓库用 ffmpeg 抽首帧。
    /// </summary>
    public static async Task<Bitmap> GetVideoThumbnailAsync(string videoPath, CancellationToken cancellation = default)
    {
        await _semaphore.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            string cachedPath = Path.Combine(CacheFolder, GetCachedThumbnailName(videoPath) + ".jpg");
            if (File.Exists(cachedPath))
            {
                return new Bitmap(cachedPath);
            }
            Directory.CreateDirectory(CacheFolder);
            await Task.Delay(300, cancellation).ConfigureAwait(false);

            var result = await ProcessRunner.RunAsync("ffmpeg",
                ["-hide_banner", "-loglevel", "error", "-y", "-ss", "0", "-i", videoPath,
                 "-frames:v", "1", "-q:v", "4", cachedPath],
                cancellation).ConfigureAwait(false);
            if (result.ExitCode != 0 || !File.Exists(cachedPath))
            {
                throw new InvalidOperationException($"ffmpeg failed to extract video thumbnail ({result.ExitCode}): {result.StandardError.Trim()}");
            }
            return new Bitmap(cachedPath);
        }
        finally
        {
            _semaphore.Release();
        }
    }


    /// <summary>
    /// 清空缩略图缓存。
    /// </summary>
    public static void ClearThumbnailCache()
    {
        if (Directory.Exists(CacheFolder))
        {
            Directory.Delete(CacheFolder, true);
        }
    }


    private static SKBitmap Resize(SKBitmap source, int targetWidth, int targetHeight)
    {
        targetWidth = Math.Max(1, targetWidth);
        targetHeight = Math.Max(1, targetHeight);
        var target = new SKBitmap(new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var sourcePixmap = source.PeekPixels())
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.Transparent);
            using var image = SKImage.FromPixels(sourcePixmap);
            canvas.DrawImage(image, new SKRect(0, 0, targetWidth, targetHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        return target;
    }


    /// <summary>
    /// 判断图片是否是带 alpha 的 WebP（上游判据：文件头含 "VP8X" 且 ALPH 标志位）。
    /// </summary>
    private static bool HasAlpha(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> header = stackalloc byte[21];
            if (fs.Read(header) < 21)
            {
                return false;
            }
            return header[..4].SequenceEqual("RIFF"u8)
                && header[8..12].SequenceEqual("WEBP"u8)
                && header[12..16].SequenceEqual("VP8X"u8)
                && (header[20] & 0x10) != 0;
        }
        catch
        {
            return false;
        }
    }


    private static bool IsExecutableAvailable(string fileName)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }
        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(directory, fileName)))
                {
                    return true;
                }
            }
            catch
            {
            }
        }
        return false;
    }

}
