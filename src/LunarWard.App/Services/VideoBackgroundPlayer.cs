using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace LunarWard.App.Services;

/// <summary>
/// 动态背景播放器：用 <c>ffmpeg</c> 子进程把视频解成 BGRA 原始帧，
/// 逐帧写进 <see cref="WriteableBitmap"/> 再由 <see cref="Image"/> 显示。
/// </summary>
/// <remarks>
/// <para>
/// 为什么不用 VLC：<c>LibVLCSharp.Avalonia</c> 的 <c>VideoView</c> 依赖把原生 X11
/// 子窗口塞进 Avalonia 窗口，Wayland 会话里没有可嵌入的 X11 子窗口（背景会全黑）。
/// 即使改用 LibVLC 的 vmem 帧回调，Arch 系发行版把 VLC 拆成 <c>vlc-plugin-*</c>
/// 多个包，缺 <c>vlc-plugin-ffmpeg</c> 时连 H.264 都解不了，用户装没装、装得全不全都不确定。
/// </para>
/// <para>
/// 直接用 <c>ffmpeg</c> 就绕开这两件事：它是 Wayland 无关的纯进程，解码能力随发行版
/// 的 ffmpeg 包，绝大多数桌面都已存在；缺了也只是回退静态图，不会黑屏。
/// 帧格式固定 1280×720 BGRA：够 1200 宽窗口用，又不必把 1080p 的每帧 8 MiB 拷来拷去。
/// </para>
/// <para>
/// 有 <c>/dev/dri/renderD128</c> 时优先走 VAAPI 硬解（软件只做一次缩放/下载），
/// 因为官方动态背景是 VP9 1080p，纯软解实测要吃掉半个核；硬解后 ffmpeg 侧从约 50% 降到约 27%。
/// 硬解起不来（驱动/编解码器不支持）会自动重试一次纯软解，仍然失败才让调用方回退静态图。
/// </para>
/// </remarks>
public sealed class VideoBackgroundPlayer : IDisposable
{
    /// <summary>输出宽度（像素）。</summary>
    public const int Width = 1280;

    /// <summary>输出高度（像素）。</summary>
    public const int Height = 720;

    private const int FrameBytes = Width * Height * 4;

    /// <summary>输出帧率上限，避免高帧率视频把 UI 线程刷爆。</summary>
    private const int MaxFrameRate = 24;

    private const int SigStop = 19;

    private const int SigCont = 18;

    /// <summary>等待 ffmpeg 提交首帧的最长时间；超时视为视频不可用。</summary>
    private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(5);

    /// <summary>停止时等待读帧任务退出的上限；在后台线程上等，不阻塞 UI。</summary>
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1.5);

    private readonly WriteableBitmap _bitmap;

    private readonly Image _image;

    private Process? _process;

    private CancellationTokenSource? _cancellation;

    private Task? _pump;

    private TaskCompletionSource? _firstFrame;

    private bool _paused;

    private bool _disposed;


    public VideoBackgroundPlayer()
    {
        _bitmap = new WriteableBitmap(
            new PixelSize(Width, Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        _image = new Image
        {
            Source = _bitmap,
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
    }


    /// <summary>直接放进 <c>ContentPresenter</c> 的显示控件。</summary>
    public Control View => _image;

    /// <summary>是否正在播放（子进程存活且未暂停）。</summary>
    public bool IsRunning => _process is { HasExited: false } && !_paused;

    /// <summary>本机是否有可用的 VAAPI 设备（没有就纯软解）。</summary>
    public static bool IsVaapiAvailable
        => Environment.GetEnvironmentVariable("LUNARWARD_FFMPEG_NO_HWACCEL") is not ("1" or "true")
           && File.Exists("/dev/dri/renderD128");

    /// <summary>本次播放是否走了硬件解码（日志/诊断用）。</summary>
    public bool IsHardwareAccelerated { get; private set; }


    /// <summary>
    /// 开始播放（本地路径或 <c>http(s)</c> 地址，循环无限）。
    /// 返回 <c>false</c> 表示 ffmpeg 不可用或启动失败。
    /// </summary>
    public async Task<bool> StartAsync(string source, LogService? log = null)
    {
        await StopAsync().ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(source) || !BackgroundService.IsFfmpegAvailable)
        {
            return false;
        }

        // 每次开播都是新的一次等待：旧的 TaskCompletionSource 会在上一次播放完成后永久 Completed，
        // 复用它会让后续所有 StartAsync 都「假成功」。
        _firstFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (IsVaapiAvailable && await TryStartAsync(source, hardware: true, log).ConfigureAwait(true))
        {
            IsHardwareAccelerated = true;
            return true;
        }

        // 硬解不起（驱动不支持该 codec / vaapi 初始化失败）就重试一次软解。
        await StopAsync().ConfigureAwait(true);
        _firstFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IsHardwareAccelerated = false;
        return await TryStartAsync(source, hardware: false, log).ConfigureAwait(true);
    }


    private async Task<bool> TryStartAsync(string source, bool hardware, LogService? log)
    {
        var uri = ToInputUri(source);
        var arguments = BuildArguments(uri, hardware);

        var startInfo = new ProcessStartInfo("ffmpeg", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            _process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            log?.Warn($"动态背景启动 ffmpeg 失败：{ex.Message}");
            _process = null;
            return false;
        }

        if (_process is null)
        {
            return false;
        }

        _cancellation = new CancellationTokenSource();
        _paused = false;

        // ffmpeg 的 stderr 只有错误，收下来写日志，便于排查「有进程没画面」。
        var process = _process;
        _ = Task.Run(async () =>
        {
            try
            {
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    log?.Warn($"动态背景 ffmpeg{(hardware ? "（VAAPI）" : string.Empty)}：{error.Trim()}");
                }
            }
            catch (Exception)
            {
                // 进程被强杀时读 stderr 会抛，忽略。
            }
        });

        _pump = Task.Run(() => PumpAsync(process, _cancellation.Token, log));

        // 等第一帧：拿不到就当视频不可用，调用方回退静态图。
        var firstFrameSource = _firstFrame;
        if (firstFrameSource is null)
        {
            await StopAsync().ConfigureAwait(true);
            return false;
        }

        var firstFrame = await Task.WhenAny(firstFrameSource.Task, Task.Delay(FirstFrameTimeout))
            .ConfigureAwait(true);
        if (firstFrame != firstFrameSource.Task)
        {
            log?.Warn($"动态背景 {FirstFrameTimeout.TotalSeconds:0}s 内没有解出帧（{(hardware ? "VAAPI" : "软解")}）：{source}");
            await StopAsync().ConfigureAwait(true);
            return false;
        }

        if (firstFrameSource.Task.IsCanceled || _disposed)
        {
            return false;
        }

        return true;
    }


    private static string BuildArguments(string input, bool hardware)
    {
        var scale = "scale=" + Width + ":" + Height + ":force_original_aspect_ratio=increase"
                    + ":flags=fast_bilinear,crop=" + Width + ":" + Height
                    + ",fps=" + MaxFrameRate;

        if (hardware)
        {
            return string.Join(' ',
                "-hide_banner",
                "-loglevel error",
                "-nostdin",
                "-re",
                "-stream_loop -1",
                "-hwaccel vaapi",
                "-hwaccel_output_format vaapi",
                "-vaapi_device /dev/dri/renderD128",
                $"-i {Quote(input)}",
                "-an",
                "-sn",
                "-dn",
                "-vf \"hwdownload,format=nv12," + scale + "\"",
                "-pix_fmt bgra",
                "-f rawvideo",
                "pipe:1");
        }

        return string.Join(' ',
            "-hide_banner",
            "-loglevel error",
            "-nostdin",
            "-re",
            "-stream_loop -1",
            $"-i {Quote(input)}",
            "-an",
            "-sn",
            "-dn",
            "-vf \"" + scale + "\"",
            "-pix_fmt bgra",
            "-f rawvideo",
            "pipe:1");
    }


    private async Task PumpAsync(Process process, CancellationToken cancellationToken, LogService? log)
    {
        var stream = process.StandardOutput.BaseStream;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // 帧缓冲从 ArrayPool 借，拷进 WriteableBitmap 后立刻归还：
                // 24fps×3.5MiB 如果每帧都 new，就是每秒几十 MB 的垃圾，GC 会把 UI 拖出卡顿。
                var frame = ArrayPool<byte>.Shared.Rent(FrameBytes);
                try
                {
                    var read = await ReadExactAsync(stream, frame, FrameBytes, cancellationToken).ConfigureAwait(false);
                    if (read < FrameBytes)
                    {
                        break;
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_disposed)
                        {
                            return;
                        }

                        using (var locked = _bitmap.Lock())
                        {
                            Marshal.Copy(frame, 0, locked.Address, FrameBytes);
                        }

                        _image.InvalidateVisual();
                        _firstFrame?.TrySetResult();
                    }, DispatcherPriority.Render);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(frame);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止。
        }
        catch (Exception ex)
        {
            log?.Warn($"动态背景读取帧失败：{ex.Message}");
        }
    }


    private static async Task<int> ReadExactAsync(
        Stream stream,
        byte[] buffer,
        int length,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return offset;
    }


    /// <summary>暂停（SIGSTOP）：进程挂着不耗 CPU，恢复后从原处继续。</summary>
    public void Pause()
    {
        if (_process is not { HasExited: false } || _paused)
        {
            return;
        }

        _paused = true;
        Signal(_process.Id, SigStop);
    }


    /// <summary>恢复（SIGCONT）。</summary>
    public void Resume()
    {
        if (_process is not { HasExited: false } || !_paused)
        {
            return;
        }

        _paused = false;
        Signal(_process.Id, SigCont);
    }


    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int sig);


    private static void Signal(int pid, int signal)
    {
        try
        {
            kill(pid, signal);
        }
        catch (Exception)
        {
            // 没有 libc.kill（非 Linux）或进程已退出：忽略。
        }
    }


    /// <summary>
    /// 停止播放并等待读帧循环退出。
    /// </summary>
    /// <remarks>
    /// 必须 async：读帧任务可能在 UI 线程上排着一次 <c>InvokeAsync</c>，
    /// 在 UI 线程上同步 <c>Wait</c> 会直接死等到超时（旧实现的「切一次背景卡 1 秒」就是它）。
    /// </remarks>
    public async Task StopAsync()
    {
        var pump = _pump;
        var process = _process;

        _pump = null;
        _process = null;

        try
        {
            _cancellation?.Cancel();
        }
        catch (Exception)
        {
            // ignore
        }

        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // ignore
        }

        if (pump is not null)
        {
            try
            {
                await Task.WhenAny(pump, Task.Delay(StopTimeout)).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // ignore
            }
        }

        try
        {
            _cancellation?.Dispose();
        }
        catch (Exception)
        {
            // ignore
        }

        _cancellation = null;
        _paused = false;

        try
        {
            process?.Dispose();
        }
        catch (Exception)
        {
            // ignore
        }
    }


    /// <summary>
    /// 同步停止：只发信号杀进程、不等读帧循环。
    /// 给 <c>BackgroundService.Stop()</c>（切静态图/卸载）这类必须立刻生效的路径用。
    /// </summary>
    public void Stop()
    {
        try
        {
            _cancellation?.Cancel();
        }
        catch (Exception)
        {
            // ignore
        }

        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // ignore
        }

        _process?.Dispose();
        _process = null;
        _cancellation?.Dispose();
        _cancellation = null;
        _paused = false;

        // 不等待 _pump：它可能正卡在 Dispatcher.InvokeAsync 上，等它就是之前 UI 卡顿的来源。
        _pump = null;
    }


    private static string ToInputUri(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var absolute)
            && absolute.Scheme is "http" or "https")
        {
            return source;
        }

        return Path.GetFullPath(source);
    }


    private static string Quote(string value) => value.Contains(' ') ? $"\"{value}\"" : value;


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose 不再等读帧任务（可能就在 UI 线程上）：直接杀进程，句柄由进程对象自己清。
        try
        {
            _cancellation?.Cancel();
        }
        catch (Exception)
        {
            // ignore
        }

        try
        {
            _process?.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            // ignore
        }

        try
        {
            _firstFrame?.TrySetCanceled();
        }
        catch (Exception)
        {
            // ignore
        }

        _process?.Dispose();
        _process = null;
        _cancellation?.Dispose();
        _cancellation = null;
        _pump = null;
    }
}
