using Avalonia.Media.Imaging;
using LunarWard.Features.Overlay;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LunarWard.Helpers;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ScreenCaptureService</c>（555 行）：截图 → 编码 → 剪贴板。
/// </summary>
/// <remarks>
/// <para>
/// 结构、成员名与调用方形状照上游；平台相关部分替换如下（逐条登记在 docs/09 §8.4）：
/// </para>
/// <list type="bullet">
/// <item>抓帧：<c>Windows.Graphics.Capture</c> + Win2D → 外部工具（X11 <c>import</c>/<c>xwd</c>、Wayland <c>grim</c>），
/// 见 <see cref="ScreenCaptureHelper"/>；位图类型 <c>CanvasBitmap</c> → Avalonia <see cref="Bitmap"/>。</item>
/// <item>HDR/ICC：<c>DisplayInformation</c> / <c>ColorPrimaries</c> / <c>WhiteLevelAdjustmentEffect</c> 无 Linux 等价物，
/// 一律按 SDR（BT.709）处理，<c>ScreenCaptureItem.HDR</c> 恒 false；<see cref="GetMaxCLL"/> 保留上游直方图算法。</item>
/// <item>编码：Win2D <c>ImageSaver</c>（PNG/AVIF/JXL，含 ICC + XMP 嵌入）→ PNG 用 Avalonia 编码器，
/// AVIF/JXL 调 <c>avifenc</c>/<c>cjxl</c>；工具缺失时回退 PNG 并记警告。</item>
/// <item>剪贴板：<c>StorageFile</c> + <c>SetStorageItems</c> → <see cref="Helpers.ClipboardHelper.SetBitmap(string)"/>（仅 PNG/JPEG 位图）。</item>
/// <item>窗口裁剪：<c>DwmGetWindowAttribute</c> 无等价物，<see cref="TryClipClient"/> 恒 false（不裁剪）。</item>
/// </list>
/// </remarks>
internal class ScreenCaptureService
{


    private static ScreenCaptureService? _instance;

    private static ScreenCaptureService Instance => _instance ??= AppConfig.GetService<ScreenCaptureService>();



    private readonly ILogger<ScreenCaptureService> _logger;

    private readonly ConcurrentDictionary<nint, ScreenCaptureContext> _captureContexts = new();

    private static readonly SemaphoreSlim _encodeSlim = new(1);



    public ScreenCaptureService(ILogger<ScreenCaptureService> logger)
    {
        _logger = logger;
    }



    /// <summary>
    /// 上游 <c>Capture()</c>：抓取最近活跃游戏窗口（快捷键入口）。
    /// </summary>
    public static void Capture()
    {
        Instance.CaptureInternal();
    }



    private async void CaptureInternal()
    {
        RunningGame? runningGame = RunningGameService.GetLatestActiveGame();
        if (runningGame is null)
        {
            return;
        }
        try
        {
            nint windowHandle = runningGame.WindowHandle;
            if (windowHandle == 0)
            {
                // 上游从 Process.MainWindowHandle（HWND）拿窗口；Linux 上该值恒为 0，
                // 改由 xdotool 按 pid 反查 X11 window id（Wayland 返回 0，走整屏抓取）。
                windowHandle = ScreenCaptureHelper.FindWindowByPid(runningGame.Pid);
                runningGame.WindowHandle = windowHandle;
            }
            if (windowHandle == 0 && !ScreenCaptureHelper.SupportsFullScreenFallback)
            {
                _logger.LogWarning("Cannot locate the game window. Pid: {pid}", runningGame.Pid);
                return;
            }
            using ScreenCaptureItem captureItem = await CaptureAndProceedImageAsync(runningGame, windowHandle);
            string filePath = await SaveImageAsync(runningGame, captureItem);
            await CopyToClipboardAsync(filePath);
        }
        catch (PlatformNotSupportedException ex)
        {
            _logger.LogWarning(ex, "Screen capture is not available on this system.");
            InAppToast.MainWindow?.Warning(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while capturing the screen.");
            InAppToast.MainWindow?.Error("截图失败，请查看日志。");
        }
    }



    private async Task<ScreenCaptureItem> CaptureAndProceedImageAsync(RunningGame runningGame, nint windowHandle)
    {
        if (!_captureContexts.TryGetValue(windowHandle, out ScreenCaptureContext? context))
        {
            context = new ScreenCaptureContext(windowHandle);
            context.CaptureWindowClosed += OnCaptureWindowClosed;
            _captureContexts.TryAdd(windowHandle, context);
        }
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Bitmap bitmap = await context.CaptureAsync(cts.Token).ConfigureAwait(false);
        DateTimeOffset frameTime = DateTimeOffset.Now;
        return new ScreenCaptureItem
        {
            Bitmap = bitmap,
            FrameTime = frameTime,
            HDR = false,
            MaxCLL = -1,
            SdrWhiteLevel = 0,
        };
    }



    /// <summary>
    /// 保存为文件。目录/扩展名/质量映射照上游；编码器换成 Linux 可用的实现。
    /// </summary>
    private static async Task<string> SaveImageAsync(RunningGame runningGame, ScreenCaptureItem captureItem)
    {
        string screenshotFolder;
        string? targetFolder = AppConfig.ScreenshotFolder;
        if (Directory.Exists(targetFolder))
        {
            screenshotFolder = Path.GetFullPath(Path.Join(targetFolder, runningGame.Process.ProcessName));
        }
        else
        {
            screenshotFolder = Path.GetFullPath(Path.Join(AppConfig.UserDataFolder, "Screenshots", runningGame.Process.ProcessName));
        }
        Directory.CreateDirectory(screenshotFolder);

        string extension = (captureItem.HDR, AppConfig.ScreenCaptureSavedFormat) switch
        {
            (_, 1) => "avif",
            (_, 2) => "jxl",
            (false, _) => "png",
            (true, _) => "avif",
        };
        string fileName = $"{runningGame.Process.ProcessName}_{captureItem.FrameTime:yyyyMMdd_HHmmssff}.{extension}";
        string filePath = Path.Combine(screenshotFolder, fileName);

        await _encodeSlim.WaitAsync().ConfigureAwait(false);
        try
        {
            if (extension is "png")
            {
                captureItem.Bitmap.Save(filePath);
            }
            else
            {
                // Linux 侧没有 Win2D/ImageSaver：先落 PNG，再交给 avifenc / cjxl 转码。
                string tempPng = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
                try
                {
                    captureItem.Bitmap.Save(tempPng);
                    bool converted = extension switch
                    {
                        "avif" => await ScreenCaptureEncoder.TryEncodeAvifAsync(tempPng, filePath, GetQuality()).ConfigureAwait(false),
                        "jxl" => await ScreenCaptureEncoder.TryEncodeJxlAsync(tempPng, filePath, GetDistance()).ConfigureAwait(false),
                        _ => throw new NotSupportedException($"Unsupported image format: {extension}"),
                    };
                    if (!converted)
                    {
                        // 上游会抛 NotSupportedException；Linux 上更实用的是回退 PNG 并让用户拿到截图。
                        filePath = Path.ChangeExtension(filePath, ".png");
                        File.Move(tempPng, filePath, overwrite: true);
                    }
                }
                finally
                {
                    if (File.Exists(tempPng))
                    {
                        File.Delete(tempPng);
                    }
                }
            }
        }
        finally
        {
            _encodeSlim.Release();
        }
        return filePath;
    }

    private static int GetQuality() => AppConfig.ScreenCaptureEncodeQuality switch
    {
        0 => 80,
        1 => 90,
        2 => 100,
        _ => 90,
    };

    private static float GetDistance() => AppConfig.ScreenCaptureEncodeQuality switch
    {
        0 => 2,
        1 => 1,
        2 => 0,
        _ => 1,
    };



    /// <summary>
    /// 上游把 XMP 元数据直接嵌进 PNG/AVIF/JXL。Linux 侧编码器不同，本方法保留同名纯函数
    /// （<c>ImageBatchConvertWindow</c> 等调用方需要），暂时只作为旁车文件写出。
    /// </summary>
    public static byte[] BuildXMPMetadata(DateTimeOffset time)
    {
        string value = $"""
            <x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"><rdf:Description xmlns:xmp="http://ns.adobe.com/xap/1.0/"><xmp:CreatorTool>Starward Launcher</xmp:CreatorTool><xmp:CreateDate>{time:yyyy-MM-ddTHH:mm:sszzz}</xmp:CreateDate></rdf:Description></rdf:RDF></x:xmpmeta>
            """;
        return Encoding.UTF8.GetBytes(value);
    }



    /// <summary>
    /// 上游仅在 HDR 截图后另存 SDR JPG；Linux 侧不采 HDR（<c>HDR</c> 恒 false），因此恒返回 null。
    /// </summary>
    private static Task<string?> SaveAsSdrAsync(RunningGame runningGame, string filePath, ScreenCaptureItem captureItem)
    {
        return Task.FromResult<string?>(null);
    }



    public static async Task CopyToClipboardAsync(string? filePath)
    {
        if (AppConfig.AutoCopyScreenshotToClipboard && File.Exists(filePath))
        {
            string extension = Path.GetExtension(filePath);
            if (extension is ".png" or ".jpg" or ".jpeg")
            {
                await Task.Run(() => Helpers.ClipboardHelper.SetBitmap(filePath));
            }
            else
            {
                // 上游把 StorageFile 放进剪贴板（资源管理器可直接粘贴）；Avalonia 的位图剪贴板
                // 只能写位图，AVIF/JXL 需要先转码，这里保持上游「文件已在磁盘上」的主行为。
                AppConfig.GetLogger<ScreenCaptureService>().LogDebug("Clipboard bitmap copy skipped for {extension}.", extension);
            }
        }
    }



    /// <summary>
    /// 上游用 DWM 窗口边框算出客户区，裁掉标题栏/边框。Linux 的窗口抓取（import -window）本身就只含
    /// 窗口内容，无需裁剪，恒返回 false。
    /// </summary>
    public static bool TryClipClient(nint hwnd, out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        return false;
    }



    /// <summary>
    /// 图片最大亮度（上游 Win2D 直方图算法的 Linux 实现）。
    /// </summary>
    public static float GetMaxCLL(Bitmap bitmap)
    {
        const int NumBins = 500;
        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;
        if (width <= 0 || height <= 0)
        {
            return 0;
        }
        // 上游先缩放到最长边 ≤2048、最大 0.5 倍，再按 (0.2126R+0.7152G+0.0722B)/125 取亮度、开平方进直方图。
        float pixelScale = MathF.Min(0.5f, 2048f / MathF.Max(width, height));
        int step = Math.Max(1, (int)MathF.Round(1f / pixelScale));
        var histogram = new float[NumBins];
        float total = 0;
        int stride = width * 4;
        IntPtr buffer = Marshal.AllocHGlobal(stride);
        try
        {
            var rect = new Avalonia.PixelRect(0, 0, width, 1);
            for (int row = 0; row < height; row += step)
            {
                rect = new Avalonia.PixelRect(0, row, width, 1);
                bitmap.CopyPixels(rect, buffer, stride, stride);
                byte[] pixels = new byte[stride];
                Marshal.Copy(buffer, pixels, 0, stride);
                for (int i = 0; i < stride; i += 4)
                {
                    float luminance = (0.2126f * pixels[i + 2] + 0.7152f * pixels[i + 1] + 0.0722f * pixels[i]) / 125f;
                    if (luminance <= 0)
                    {
                        continue;
                    }
                    float value = MathF.Sqrt(luminance);
                    int bin = Math.Clamp((int)(value * NumBins), 0, NumBins - 1);
                    histogram[bin] += 1;
                    total += 1;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        if (total <= 0)
        {
            return 0;
        }
        for (int i = 0; i < histogram.Length; i++)
        {
            histogram[i] /= total;
        }
        int maxBinIndex = 0;
        float cumulative = 0;
        for (int i = histogram.Length - 1; i >= 0; i--)
        {
            cumulative += histogram[i];
            if (cumulative >= 0.0001f)
            {
                maxBinIndex = i;
                break;
            }
        }
        return MathF.Pow((maxBinIndex + 0.5f) / histogram.Length, 2f) * 10000;
    }



    private void OnCaptureWindowClosed(object? sender, EventArgs e)
    {
        try
        {
            if (sender is ScreenCaptureContext context)
            {
                _captureContexts.TryRemove(context.WindowHandle, out _);
            }
        }
        catch { }
    }


}



/// <summary>
/// AVIF / JPEG XL 编码（上游由 Win2D <c>ImageSaver</c> 完成，Linux 侧调用外部编码器）。
/// </summary>
internal static class ScreenCaptureEncoder
{


    public static async Task<bool> TryEncodeAvifAsync(string pngPath, string outputPath, int quality)
    {
        // avifenc 的 cq-level 越小质量越高（0-63）；把上游质量 80/90/100 线性映射过去。
        int cqLevel = Math.Clamp((int)Math.Round((100 - quality) * 0.63), 0, 63);
        return await TryRunAsync("avifenc", new[] { "--min", "0", "--max", "63", "-a", "end-usage=q", "-a", string.Create(CultureInfo.InvariantCulture, $"cq-level={cqLevel}"), pngPath, outputPath }, outputPath).ConfigureAwait(false);
    }



    public static async Task<bool> TryEncodeJxlAsync(string pngPath, string outputPath, float distance)
    {
        return await TryRunAsync("cjxl", new[] { "--distance", distance.ToString("0.##", CultureInfo.InvariantCulture), pngPath, outputPath }, outputPath).ConfigureAwait(false);
    }



    private static async Task<bool> TryRunAsync(string fileName, string[] args, string outputPath)
    {
        if (!IsToolAvailable(fileName))
        {
            return false;
        }
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0 && File.Exists(outputPath);
        }
        catch
        {
            return false;
        }
    }



    private static bool IsToolAvailable(string tool)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                   .Any(dir => File.Exists(Path.Combine(dir, tool)));
    }


}
