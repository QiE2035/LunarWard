using System;
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
/// 多个包，<c>vc</c> 缺 <c>vlc-plugin-ffmpeg</c> 时连 H.264 都解不了
/// （实测 <c>Codec 'h264' is not supported</c>），用户装没装、装得全不全都不确定。
/// </para>
/// <para>
/// 直接用 <c>ffmpeg</c> 就绕开这两件事：它是 Wayland 无关的纯进程，解码能力随发行版
/// 的 ffmpeg 包，绝大多数桌面都已存在；缺了也只是回退静态图，不会黑屏。
/// 帧格式固定 1280×720 BGRA：够 1200 宽窗口用，又不必把 1080p 的每帧 8 MiB 拷来拷去。
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

    private readonly WriteableBitmap _bitmap;

    private readonly Image _image;

    private Process? _process;

    private CancellationTokenSource? _cancellation;

    private Task? _pump;

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


    /// <summary>
    /// 开始播放（本地路径或 <c>http(s)</c> 地址，循环无限）。
    /// 返回 <c>false</c> 表示 ffmpeg 不可用或启动失败。
    /// </summary>
    public async Task<bool> StartAsync(string source, LogService? log = null)
    {
        Stop();

        if (string.IsNullOrWhiteSpace(source) || !BackgroundService.IsFfmpegAvailable)
        {
            return false;
        }

        var uri = ToInputUri(source);
        var arguments = string.Join(' ',
            "-hide_banner",
            "-loglevel error",
            "-nostdin",
            "-re",
            "-stream_loop -1",
            $"-i {Quote(uri)}",
            "-an",
            "-sn",
            "-dn",
            "-vf \"scale=1280:720:force_original_aspect_ratio=increase,crop=1280:720,fps=" + MaxFrameRate + "\"",
            "-pix_fmt bgra",
            "-f rawvideo",
            "pipe:1");

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
        _ = Task.Run(async () =>
        {
            try
            {
                var error = await _process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    log?.Warn($"动态背景 ffmpeg：{error.Trim()}");
                }
            }
            catch (Exception)
            {
                // 进程被强杀时读 stdout/stderr 会抛，忽略。
            }
        });

        _pump = Task.Run(() => PumpAsync(_process, _cancellation.Token, log));

        // 等第一帧（最多 3s）：拿不到就当视频不可用，调用方回退静态图。
        var firstFrame = await Task.WhenAny(_firstFrame.Task, Task.Delay(TimeSpan.FromSeconds(3)))
            .ConfigureAwait(true);
        if (firstFrame != _firstFrame.Task)
        {
            log?.Warn($"动态背景 3s 内没有解出帧：{source}");
            Stop();
            return false;
        }

        return true;
    }


    private readonly TaskCompletionSource _firstFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);


    private async Task PumpAsync(Process process, CancellationToken cancellationToken, LogService? log)
    {
        var buffer = new byte[FrameBytes];
        var stream = process.StandardOutput.BaseStream;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await ReadExactAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
                if (read < FrameBytes)
                {
                    break;
                }

                var frame = buffer;
                buffer = new byte[FrameBytes];

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
                    if (!_firstFrame.Task.IsCompleted)
                    {
                        _firstFrame.TrySetResult();
                    }
                });
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


    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
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

        if (_pump is not null)
        {
            try
            {
                _pump.Wait(TimeSpan.FromSeconds(1));
            }
            catch (Exception)
            {
                // ignore
            }

            _pump = null;
        }
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
        Stop();
        try
        {
            _firstFrame.TrySetCanceled();
        }
        catch (Exception)
        {
            // ignore
        }
    }
}
