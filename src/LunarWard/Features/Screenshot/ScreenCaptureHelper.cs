using Avalonia.Media.Imaging;
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
/// 与 WinRT COM 互操作抓取窗口/显示器，Linux 没有等价 API，这里改为调用外部工具抓帧，
/// 结果统一解码成 Avalonia <see cref="Bitmap"/>（详细差异见 docs/09 §8.4）：
/// </para>
/// <list type="bullet">
/// <item>X11 窗口：<c>xdotool search --pid &lt;pid&gt;</c> 定位窗口 id → <c>import -window &lt;id&gt; png:-</c>
/// （ImageMagick）；缺 import 时退 <c>xwd</c> + <c>convert</c>。</item>
/// <item>Wayland：<c>grim -</c> 只能抓整个输出（无按窗口抓取协议），按窗口抓取退化为全屏抓取并记警告；
/// 指定区域时用 <c>grim -g x,y wxh</c>。</item>
/// <item>工具缺失时抛 <see cref="PlatformNotSupportedException"/>（UI 按上游「截图不可用」处理，不静默出黑图）。</item>
/// </list>
/// </remarks>
internal static class ScreenCaptureHelper
{


    private static bool IsWayland => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));


    /// <summary>
    /// 是否可以在「找不到具体窗口」时退化为整屏抓取（Wayland 没有按窗口抓取协议，grim 只能抓输出）。
    /// X11 找不到窗口时视为窗口已关闭，不再抓整屏，避免截到错误的画面。
    /// </summary>
    public static bool SupportsFullScreenFallback => IsWayland && _hasGrim.Value;


    private static readonly Lazy<bool> _hasImport = new(() => HasTool("import"));

    private static readonly Lazy<bool> _hasGrim = new(() => HasTool("grim"));

    private static readonly Lazy<bool> _hasXdotool = new(() => HasTool("xdotool"));



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
    /// 按进程查找可见窗口 id（X11：xdotool）。Wayland 没有公开的跨进程窗口枚举协议，返回 0。
    /// </summary>
    public static nint FindWindowByPid(int pid)
    {
        if (IsWayland || !_hasXdotool.Value)
        {
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
            catch { }
        }
        return 0;
    }



    /// <summary>
    /// 上游 <c>CaptureWindowAsync(nint hwnd, DirectXPixelFormat, CancellationToken)</c>。
    /// </summary>
    public static async Task<Bitmap> CaptureWindowAsync(nint windowHandle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (windowHandle == 0)
        {
            if (IsWayland && _hasGrim.Value)
            {
                // Wayland 没有按窗口抓取的协议；退化为抓整个输出。
                return await CaptureMonitorAsync(cancellationToken).ConfigureAwait(false);
            }
            throw new PlatformNotSupportedException("无法定位游戏窗口（Linux 侧需要 X11 + xdotool/import 或 Wayland + grim）。");
        }
        if (_hasImport.Value)
        {
            byte[] png = await RunToolBytesAsync("import", new[] { "-window", windowHandle.ToString(CultureInfo.InvariantCulture), "png:-" }, cancellationToken).ConfigureAwait(false);
            return Decode(png);
        }
        if (HasTool("xwd") && HasTool("convert"))
        {
            byte[] xwd = await RunToolBytesAsync("xwd", new[] { "-silent", "-id", windowHandle.ToString(CultureInfo.InvariantCulture) }, cancellationToken).ConfigureAwait(false);
            byte[] png = await RunToolBytesWithInputAsync("convert", new[] { "xwd:-", "png:-" }, xwd, cancellationToken).ConfigureAwait(false);
            return Decode(png);
        }
        throw new PlatformNotSupportedException("截图需要 ImageMagick 的 import（或 xwd + convert）；未检测到可用的抓屏工具。");
    }



    /// <summary>
    /// 上游 <c>CaptureMonitorAsync(nint monitor, DirectXPixelFormat, CancellationToken)</c>。
    /// Linux 侧没有 HMONITOR 句柄，直接抓整个输出（Wayland: grim，X11: import root）。
    /// </summary>
    public static async Task<Bitmap> CaptureMonitorAsync(CancellationToken cancellationToken = default)
    {
        if (_hasGrim.Value && IsWayland)
        {
            byte[] png = await RunToolBytesAsync("grim", new[] { "-" }, cancellationToken).ConfigureAwait(false);
            return Decode(png);
        }
        if (_hasImport.Value)
        {
            byte[] png = await RunToolBytesAsync("import", new[] { "-window", "root", "png:-" }, cancellationToken).ConfigureAwait(false);
            return Decode(png);
        }
        throw new PlatformNotSupportedException("截图需要 Wayland 的 grim 或 ImageMagick 的 import；未检测到可用的抓屏工具。");
    }



    /// <summary>
    /// 抓取指定区域（Wayland: <c>grim -g x,y wxh</c>；X11: <c>import -window root -crop</c>）。
    /// </summary>
    public static async Task<Bitmap> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken cancellationToken = default)
    {
        if (_hasGrim.Value && IsWayland)
        {
            string geometry = string.Create(CultureInfo.InvariantCulture, $"{x},{y} {width}x{height}");
            byte[] png = await RunToolBytesAsync("grim", new[] { "-g", geometry, "-" }, cancellationToken).ConfigureAwait(false);
            return Decode(png);
        }
        if (_hasImport.Value)
        {
            string geometry = string.Create(CultureInfo.InvariantCulture, $"{width}x{height}+{x}+{y}");
            byte[] png = await RunToolBytesAsync("import", new[] { "-window", "root", "-crop", geometry, "png:-" }, cancellationToken).ConfigureAwait(false);
            return Decode(png);
        }
        throw new PlatformNotSupportedException("区域截图需要 grim 或 ImageMagick 的 import。");
    }



    private static Bitmap Decode(byte[] png)
    {
        if (png is null || png.Length == 0)
        {
            throw new InvalidOperationException("抓屏工具没有返回图像数据。");
        }
        return new Bitmap(new MemoryStream(png));
    }



    private static (int ExitCode, string Output) RunTool(string fileName, IEnumerable<string> args)
    {
        using var process = StartProcess(fileName, args);
        process.WaitForExit();
        return (process.ExitCode, process.StandardOutput.ReadToEnd());
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
