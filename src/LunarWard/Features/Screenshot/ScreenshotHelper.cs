using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ScreenshotHelper</c>：截图文件的扩展名判定、写入完成等待与转码。
/// </summary>
/// <remarks>
/// 平台差异（见 docs/09）：
/// <list type="number">
/// <item>上游用 <c>Kernel32.CreateFile2</c> 独占打开来判断游戏是否写完文件；Linux 没有强制共享锁，
/// 改成「能读到 + 文件大小稳定」的轮询（30 × 100 ms，与上游次数一致）。</item>
/// <item>上游用 WIC <c>BitmapDecoder/BitmapEncoder</c> 转 jpg，并自带 Windows 版 <c>avifdec.exe</c>/<c>djxl.exe</c>；
/// Linux 侧优先调用系统里的 <c>avifdec</c>/<c>djxl</c>（与上游同名工具），否则回退 <c>ffmpeg</c>，
/// 都没有时抛 <see cref="PlatformNotSupportedException"/> 并给出安装提示（不静默返回错误文件）。</item>
/// </list>
/// </remarks>
internal static class ScreenshotHelper
{

    public static bool IsSupportedExtension(string file)
    {
        return Path.GetExtension(file) is ".jpg" or ".png" or ".jxr" or ".webp" or ".heic" or ".avif" or ".jxl";
    }



    public static List<string> WatcherFilters { get; } = new List<string> { "*.jpg", "*.png", "*.jxr", "*.webp", "*.heic", "*.avif", "*.jxl" };



    public static async Task WaitForFileReleaseAsync(string filePath, CancellationToken cancellation = default)
    {
        int count = 0;
        long lastLength = -1;
        while (count < 30)
        {
            try
            {
                var info = new FileInfo(filePath);
                if (!info.Exists)
                {
                    await Task.Delay(100, cancellation);
                    count++;
                    continue;
                }
                using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long length = stream.Length;
                if (length > 0 && length == lastLength)
                {
                    break;
                }
                lastLength = length;
            }
            catch (Exception)
            {
            }
            await Task.Delay(100, cancellation);
            count++;
        }
    }


    /// <summary>上游：<c>Path.Combine(AppContext.BaseDirectory, "avifdec.exe")</c>。</summary>
    private static string avifdec = "avifdec";

    /// <summary>上游：<c>Path.Combine(AppContext.BaseDirectory, "djxl.exe")</c>。</summary>
    private static string djxl = "djxl";


    public static async Task<string> ConvertToJpgAsync(string filePath)
    {
        if (Path.GetExtension(filePath).Equals(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            return filePath;
        }
        string cacheFolder = Path.Combine(AppConfig.CacheFolder, "cache");
        Directory.CreateDirectory(cacheFolder);
        string jpgFilePath = Path.Combine(cacheFolder, Path.GetFileNameWithoutExtension(filePath) + ".jpg");
        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension is ".avif" && File.Exists(avifdec))
        {
            await RunToolAsync(avifdec, $"""
                  "{filePath}" "{jpgFilePath}" -q 90
                  """, "avifdec");
        }
        else if (extension is ".jxl" && File.Exists(djxl))
        {
            await RunToolAsync(djxl, $"""
                  "{filePath}" "{jpgFilePath}" -q 90
                  """, "djxl");
        }
        else if (await RunToolAsync("ffmpeg", $"""
                  -y -i "{filePath}" -q:v 2 "{jpgFilePath}"
                  """, "ffmpeg", throwOnError: false) is false)
        {
            // Linux 等价实现：Avalonia 的解码器没有 jpg 编码器，这里回退 ImageMagick 的 convert/magick。
            bool converted = await RunToolAsync("magick", $"""
                  "{filePath}" -quality 90 "{jpgFilePath}"
                  """, "magick", throwOnError: false);
            if (!converted)
            {
                converted = await RunToolAsync("convert", $"""
                      "{filePath}" -quality 90 "{jpgFilePath}"
                      """, "convert", throwOnError: false);
            }
            if (!converted)
            {
                throw new PlatformNotSupportedException("Converting screenshots to jpg requires one of: avifdec, djxl, ffmpeg, imagemagick (magick/convert).");
            }
        }
        return jpgFilePath;
    }



    /// <summary>运行外部转码工具；<paramref name="throwOnError"/> 为 false 时用返回值表示「工具不存在或失败」。</summary>
    private static async Task<bool> RunToolAsync(string fileName, string arguments, string toolName, bool throwOnError = true)
    {
        Process? process;
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
        }
        catch (Exception)
        {
            process = null;
        }
        if (process is null)
        {
            if (throwOnError)
            {
                throw new PlatformNotSupportedException($"{toolName} is required to convert this image.");
            }
            return false;
        }
        using (process)
        {
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                if (throwOnError)
                {
                    throw new Exception($"{toolName} failed: {error}");
                }
                return false;
            }
        }
        return true;
    }


}
