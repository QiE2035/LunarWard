using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.Features.Screenshot;

/// <summary>
/// 上游 <c>Starward.Features.Screenshot.ScreenCaptureHelper</c>（138 行）的 Linux 抓屏后端。
/// </summary>
/// <remarks>
/// <para>
/// 上游用 <c>Windows.Graphics.Capture</c>（<c>GraphicsCaptureItem</c> + <c>Direct3D11CaptureFramePool</c>）
/// 与 WinRT COM 互操作抓取窗口/显示器；Linux 没有等价的统一 API，只有一组各自为政的外部工具，
/// 因此这里实现「工具回退链」：每一级失败只记日志，然后换下一级，全部失败才抛
/// <see cref="PlatformNotSupportedException"/>（UI 按上游「截图不可用」处理，不会静默出黑图）。
/// </para>
/// <para>
/// 整屏抓取链（窗口抓取失败时也会退到这条链）：
/// <list type="number">
/// <item><c>grim -</c>：Wayland 合成器通用抓屏（全输出）。</item>
/// <item><c>spectacle -b -n -f -o &lt;file&gt;</c>：KDE Plasma 自带抓屏（Wayland/X11 都可）。</item>
/// <item><c>gnome-screenshot -f &lt;file&gt;</c>：GNOME 抓屏。</item>
/// <item><c>import -window root png:-</c>：ImageMagick 抓 X11 root 窗口。</item>
/// <item><c>xwd -root</c> + <c>convert xwd:- png:-</c>：X11 原生 <c>xwd</c> 兜底。</item>
/// </list>
/// 窗口级抓取：<c>xdotool search --pid</c> 定位 window id → <c>import -window &lt;id&gt;</c> → <c>xwd -id</c> + <c>convert</c>。
/// 区域抓取：<c>grim -g x,y wxh</c> / <c>import -window root -crop</c>（<c>spectacle</c> 与
/// <c>gnome-screenshot</c> 没有非交互的区域参数，不参与该分支）。
/// </para>
/// <para>逐条差异登记在 docs/09「二十六、Features/Overlay 收尾」之后的抓屏小节。</para>
/// </remarks>
internal static class ScreenCaptureHelper
{


    private static readonly ILogger? _logger = TryGetLogger();


    private static ILogger? TryGetLogger()
    {
        try
        {
            // 静态类不能作为 ILogger<T> 的类型参数（CS0718）：用分类名建 logger。
            return AppConfig.GetService<ILoggerFactory>()
                .CreateLogger("LunarWard.Features.Screenshot.ScreenCaptureHelper");
        }
        catch (Exception)
        {
            // 静态初始化早于 DI 容器时（或单元测试里）不拦路，抓屏本身不依赖日志。
            return null;
        }
    }



    /// <summary>是否有 Wayland 会话（<c>WAYLAND_DISPLAY</c> 存在即认为在 Wayland 下，可能同时是 XWayland 客户端）。</summary>
    private static bool IsWayland => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));


    /// <summary>XWayland 会话：既有 Wayland 合成器又有 X11 显示，X 客户端仍可被 xdotool/import 抓到。</summary>
    private static bool IsXWayland => IsWayland && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));


    /// <summary>
    /// 是否可以在「找不到具体窗口」时退化为整屏抓取。
    /// 上游 <c>GraphicsCaptureItem</c> 拿不到窗口句柄就直接失败；Linux 侧只要有任一整屏抓屏工具就退整屏，
    /// 因为 Wayland 没有按窗口抓取的通用协议，X11 下窗口可能已关闭——是否接受整屏由调用方判断。
    /// </summary>
    public static bool SupportsFullScreenFallback => HasFullScreenCaptureTool;


    private static bool HasFullScreenCaptureTool =>
        _hasGrim.Value
        || _hasSpectacle.Value
        || _hasGnomeScreenshot.Value
        || _hasImport.Value
        || (_hasXwd.Value && _hasConvert.Value);



    private static readonly Lazy<bool> _hasGrim = new(() => HasTool("grim"));

    private static readonly Lazy<bool> _hasSpectacle = new(() => HasTool("spectacle"));

    private static readonly Lazy<bool> _hasGnomeScreenshot = new(() => HasTool("gnome-screenshot"));

    private static readonly Lazy<bool> _hasImport = new(() => HasTool("import"));

    private static readonly Lazy<bool> _hasConvert = new(() => HasTool("convert"));

    private static readonly Lazy<bool> _hasXwd = new(() => HasTool("xwd"));

    private static readonly Lazy<bool> _hasXdotool = new(() => HasTool("xdotool"));



    /// <summary>供同程序集其它模块探测外部工具（如 <c>xdotool</c>）是否存在。</summary>
    internal static bool IsToolAvailable(string tool) => HasTool(tool);


    private static bool HasTool(string tool)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }
        foreach (string dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(dir, tool)))
                {
                    return true;
                }
            }
            catch { }
        }
        return false;
    }



    /// <summary>
    /// 按进程查找可见窗口 id（X11 window id）。
    /// </summary>
    /// <remarks>
    /// 平台替换：上游用 <c>User32.EnumWindows</c> 按进程枚举顶层窗口；这里用 <c>xdotool search --pid</c>。
    /// <para>
    /// 顺序：**先试 xdotool，再判断 Wayland**。XWayland 会话里游戏可能是 X 客户端
    /// （Proton/Wine 默认就是 X11 窗口），此时 xdotool 能正常找到窗口；只有 Wayland 原生窗口
    /// 才枚举不到。若按 <c>WAYLAND_DISPLAY</c> 提前短路，XWayland 下会无故放弃可用的窗口句柄。
    /// </para>
    /// <para>真正枚举不到时返回 0，由调用方决定退整屏还是报错。</para>
    /// </remarks>
    public static nint FindWindowByPid(int pid)
    {
        // 平台替换：User32.EnumWindows + GetWindowThreadProcessId → xdotool search --pid。
        if (!_hasXdotool.Value)
        {
            _logger?.LogDebug("xdotool 不可用，无法按进程枚举窗口。Pid: {pid}", pid);
            return 0;
        }
        foreach (string[] args in new[]
        {
            new[] { "search", "--pid", pid.ToString(CultureInfo.InvariantCulture), "--onlyvisible" },
            new[] { "search", "--pid", pid.ToString(CultureInfo.InvariantCulture) },
            new[] { "search", "--class", ".*", "--pid", pid.ToString(CultureInfo.InvariantCulture) },
        })
        {
            try
            {
                (int exitCode, string output) = RunTool("xdotool", args);
                if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    // xdotool 可能返回多个 id（父/子窗口），取最后一个（通常是实际内容窗口）。
                    string? last = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                    if (nint.TryParse(last?.Trim(), out nint handle) && handle != 0)
                    {
                        return handle;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "xdotool search 失败：{args}", string.Join(' ', args));
            }
        }
        _logger?.LogDebug(
            "按 pid 找不到窗口（Wayland={wayland}，XWayland={xwayland}）。Pid: {pid}",
            IsWayland, IsXWayland, pid);
        return 0;
    }



    /// <summary>
    /// 上游 <c>CaptureWindowAsync(nint hwnd, DirectXPixelFormat, CancellationToken)</c>。
    /// </summary>
    /// <remarks>
    /// 平台替换：<c>GraphicsCaptureItem</c> + <c>Direct3D11CaptureFrame</c> → 外部工具抓帧 + 解码为 <see cref="Bitmap"/>。
    /// 窗口级失败逐级降级（import → xwd + convert → 整屏链），每级记日志。
    /// </remarks>
    public static async Task<Bitmap> CaptureWindowAsync(nint windowHandle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (windowHandle == 0)
        {
            if (SupportsFullScreenFallback)
            {
                // 平台替换：Wayland 没有按窗口抓取的协议，退化为抓整个输出（X11 下由调用方决定是否接受整屏）。
                _logger?.LogWarning("没有可用的窗口句柄，退化为整屏抓取。");
                return await CaptureMonitorAsync(cancellationToken).ConfigureAwait(false);
            }
            throw new PlatformNotSupportedException("无法定位游戏窗口，且系统里没有可用的抓屏工具（需要 grim / spectacle / gnome-screenshot / ImageMagick import 之一）。");
        }

        var failures = new List<string>(3);

        // 平台替换：GraphicsCaptureItem → import -window <id>（ImageMagick）。
        if (_hasImport.Value)
        {
            try
            {
                byte[] png = await RunToolBytesAsync(
                    "import",
                    new[] { "-window", windowHandle.ToString(CultureInfo.InvariantCulture), "png:-" },
                    cancellationToken).ConfigureAwait(false);
                return Decode("import", png);
            }
            catch (Exception ex) when (IsRecoverable(ex, cancellationToken))
            {
                failures.Add($"import: {ex.Message}");
                _logger?.LogWarning("import 抓窗口失败，尝试下一级。{message}", ex.Message);
            }
        }

        // 平台替换：Direct3D11CaptureFrame → xwd 原始 X11 抓帧 + convert 转 PNG。
        if (_hasXwd.Value && _hasConvert.Value)
        {
            try
            {
                byte[] xwd = await RunToolBytesAsync(
                    "xwd",
                    new[] { "-silent", "-id", windowHandle.ToString(CultureInfo.InvariantCulture) },
                    cancellationToken).ConfigureAwait(false);
                byte[] png = await RunToolBytesWithInputAsync(
                    "convert",
                    new[] { "xwd:-", "png:-" },
                    xwd,
                    cancellationToken).ConfigureAwait(false);
                return Decode("xwd", png);
            }
            catch (Exception ex) when (IsRecoverable(ex, cancellationToken))
            {
                failures.Add($"xwd: {ex.Message}");
                _logger?.LogWarning("xwd 抓窗口失败，尝试下一级。{message}", ex.Message);
            }
        }

        if (SupportsFullScreenFallback)
        {
            failures.Add("窗口抓取失败，已退化为整屏抓取");
            _logger?.LogWarning("窗口抓取全部失败，退化为整屏抓取。Window: {window}", windowHandle);
            return await CaptureMonitorAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new PlatformNotSupportedException($"抓取窗口失败（已尝试：{string.Join("; ", failures)}）。");
    }



    /// <summary>
    /// 上游 <c>CaptureMonitorAsync(nint monitor, DirectXPixelFormat, CancellationToken)</c>。
    /// Linux 侧没有 HMONITOR 句柄，直接按「整屏链」抓整个输出。
    /// </summary>
    /// <remarks>平台替换：<c>GraphicsCaptureItem.CreateFromDisplayId</c> → grim / spectacle / gnome-screenshot / import / xwd。</remarks>
    public static async Task<Bitmap> CaptureMonitorAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failures = new List<string>(5);

        // 平台替换：1) grim（Wayland 通用；X11 下不生效，所以只在 Wayland 试）。
        if (_hasGrim.Value && IsWayland)
        {
            try
            {
                byte[] png = await RunToolBytesAsync("grim", new[] { "-" }, cancellationToken).ConfigureAwait(false);
                return Decode("grim", png);
            }
            catch (Exception ex) when (IsRecoverable(ex, cancellationToken))
            {
                failures.Add($"grim: {ex.Message}");
                _logger?.LogWarning("grim 抓屏失败，尝试下一级。{message}", ex.Message);
            }
        }

        // 平台替换：2) spectacle（KDE Plasma，Wayland/X11 都可；非交互全屏抓取写文件）。
        if (_hasSpectacle.Value)
        {
            try
            {
                return await CaptureToTemporaryFileAsync(
                    "spectacle",
                    new[] { "-b", "-n", "-f", "-o" },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRecoverable(ex, cancellationToken))
            {
                failures.Add($"spectacle: {ex.Message}");
                _logger?.LogWarning("spectacle 抓屏失败，尝试下一级。{message}", ex.Message);
            }
        }

        // 平台替换：3) gnome-screenshot（GNOME；-f 指定输出文件，避免默认写到 ~/图片）。
        if (_hasGnomeScreenshot.Value)
        {
            try
            {
                return await CaptureToTemporaryFileAsync(
                    "gnome-screenshot",
                    new[] { "-f" },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRecoverable(ex, cancellationToken))
            {
                failures.Add($"gnome-screenshot: {ex.Message}");
                _logger?.LogWarning("gnome-screenshot 抓屏失败，尝试下一级。{message}", ex.Message);
            }
        }

        // 平台替换：4) import 抓 X11 root 窗口（ImageMagick 7.x 在部分发行版上参数解析异常，所以放最后一步并在失败时明确记录）。
        if (_hasImport.Value)
        {
            try
            {
                byte[] png = await RunToolBytesAsync("import", new[] { "-window", "root", "png:-" }, cancellationToken).ConfigureAwait(false);
                return Decode("import", png);
            }
            catch (Exception ex) when (IsRecoverable(ex, cancellationToken))
            {
                failures.Add($"import: {ex.Message}");
                _logger?.LogWarning("import 抓屏失败，尝试下一级。{message}", ex.Message);
            }
        }

        // 平台替换：5) xwd + convert（X11 原生兜底）。
        if (_hasXwd.Value && _hasConvert.Value)
        {
            try
            {
                byte[] xwd = await RunToolBytesAsync("xwd", new[] { "-silent", "-root" }, cancellationToken).ConfigureAwait(false);
                byte[] png = await RunToolBytesWithInputAsync("convert", new[] { "xwd:-", "png:-" }, xwd, cancellationToken).ConfigureAwait(false);
                return Decode("xwd", png);
            }
            catch (Exception ex) when (IsRecoverable(ex, cancellationToken))
            {
                failures.Add($"xwd: {ex.Message}");
                _logger?.LogWarning("xwd 抓屏失败。{message}", ex.Message);
            }
        }

        if (failures.Count == 0)
        {
            throw new PlatformNotSupportedException("截图需要 Wayland 的 grim、KDE 的 spectacle、GNOME 的 gnome-screenshot 或 ImageMagick 的 import；未检测到可用的抓屏工具。");
        }
        throw new PlatformNotSupportedException($"整屏抓取失败（已尝试：{string.Join("; ", failures)}）。");
    }



    /// <summary>
    /// 抓取指定区域（Wayland: <c>grim -g x,y wxh</c>；X11: <c>import -window root -crop</c>）。
    /// </summary>
    /// <remarks>
    /// 平台替换：上游按显示器句柄 + 客户区裁剪；Linux 侧用外部工具的区域参数。
    /// <c>spectacle</c>/<c>gnome-screenshot</c> 没有非交互的区域参数，不参与该分支。
    /// </remarks>
    public static async Task<Bitmap> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failures = new List<string>(2);

        if (_hasGrim.Value && IsWayland)
        {
            string geometry = string.Create(CultureInfo.InvariantCulture, $"{x},{y} {width}x{height}");
            try
            {
                byte[] png = await RunToolBytesAsync("grim", new[] { "-g", geometry, "-" }, cancellationToken).ConfigureAwait(false);
                return Decode("grim", png);
            }
            catch (Exception ex) when (IsRecoverable(ex, cancellationToken))
            {
                failures.Add($"grim: {ex.Message}");
                _logger?.LogWarning("grim 区域抓取失败，尝试下一级。{message}", ex.Message);
            }
        }

        if (_hasImport.Value)
        {
            string geometry = string.Create(CultureInfo.InvariantCulture, $"{width}x{height}+{x}+{y}");
            try
            {
                byte[] png = await RunToolBytesAsync(
                    "import",
                    new[] { "-window", "root", "-crop", geometry, "png:-" },
                    cancellationToken).ConfigureAwait(false);
                return Decode("import", png);
            }
            catch (Exception ex) when (IsRecoverable(ex, cancellationToken))
            {
                failures.Add($"import: {ex.Message}");
                _logger?.LogWarning("import 区域抓取失败。{message}", ex.Message);
            }
        }

        throw new PlatformNotSupportedException($"区域截图需要 grim 或 ImageMagick 的 import（已尝试：{string.Join("; ", failures)}）。");
    }



    /// <summary>
    /// 调用只能「写文件」的抓屏工具（spectacle / gnome-screenshot），再读回字节。
    /// </summary>
    private static async Task<Bitmap> CaptureToTemporaryFileAsync(string fileName, IEnumerable<string> argsBeforePath, CancellationToken cancellationToken)
    {
        string tempFile = Path.Join(Path.GetTempPath(), $"lunarward-capture-{Guid.NewGuid():N}.png");
        try
        {
            var args = new List<string>(argsBeforePath) { tempFile };
            (int exitCode, string error) = await RunToolAsync(fileName, args, cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"{fileName} 退出码 {exitCode}：{error}");
            }
            if (!File.Exists(tempFile))
            {
                throw new InvalidOperationException($"{fileName} 没有生成截图文件。{error}");
            }
            byte[] png = await File.ReadAllBytesAsync(tempFile, cancellationToken).ConfigureAwait(false);
            return Decode(fileName, png);
        }
        finally
        {
            try
            {
                File.Delete(tempFile);
            }
            catch { }
        }
    }



    private static bool IsRecoverable(Exception ex, CancellationToken cancellationToken)
    {
        // 用户取消要原样抛出；其它失败（工具不存在/参数不兼容/退出码非 0）都只是「这一级不可用」。
        return !cancellationToken.IsCancellationRequested && ex is not OperationCanceledException;
    }



    private static Bitmap Decode(string tool, byte[] png)
    {
        if (png is null || png.Length == 0)
        {
            throw new InvalidOperationException($"{tool} 没有返回图像数据。");
        }
        return new Bitmap(new MemoryStream(png));
    }



    private static (int ExitCode, string Output) RunTool(string fileName, IEnumerable<string> args)
    {
        using var process = StartProcess(fileName, args);
        process.WaitForExit();
        return (process.ExitCode, process.StandardOutput.ReadToEnd());
    }



    private static async Task<(int ExitCode, string Error)> RunToolAsync(string fileName, IEnumerable<string> args, CancellationToken cancellationToken)
    {
        using var process = StartProcess(fileName, args);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (process.ExitCode, error);
    }



    private static async Task<byte[]> RunToolBytesAsync(string fileName, IEnumerable<string> args, CancellationToken cancellationToken)
    {
        using var process = StartProcess(fileName, args, redirectOutput: true);
        using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{fileName} 退出码 {process.ExitCode}：{error}");
        }
        return ms.ToArray();
    }



    private static async Task<byte[]> RunToolBytesWithInputAsync(string fileName, IEnumerable<string> args, byte[] input, CancellationToken cancellationToken)
    {
        using var process = StartProcess(fileName, args, redirectOutput: true, redirectInput: true);
        await process.StandardInput.BaseStream.WriteAsync(input, cancellationToken).ConfigureAwait(false);
        await process.StandardInput.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        process.StandardInput.Close();
        using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{fileName} 退出码 {process.ExitCode}：{error}");
        }
        return ms.ToArray();
    }



    private static Process StartProcess(string fileName, IEnumerable<string> args, bool redirectOutput = true, bool redirectInput = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = true,
            RedirectStandardInput = redirectInput,
        };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }
        return Process.Start(startInfo) ?? throw new InvalidOperationException($"无法启动 {fileName}。");
    }

}
