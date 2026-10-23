using System.Collections.ObjectModel;
using LunarWard.Platform;
using Serilog;
using Serilog.Events;

namespace LunarWard.App.Services;

/// <summary>一个背景条目（视频或静态图）。</summary>
public sealed record BackgroundItem(string? VideoPath, string? ImagePath, string? Title)
{
    public bool IsVideo => VideoPath is not null;
}


/// <summary>
/// 动态背景：视频用 <c>LibVLCSharp</c>，失败时静默回退静态图。
/// </summary>
/// <remarks>
/// <para>
/// 见 <c>docs/04-FEATURES.md</c> §5.4。关键点：VLC 的 <c>EndReached</c> 回调在线程池
/// 线程上触发，必须 <c>Dispatcher.UIThread.Post</c> 回 UI 线程再重播，否则会死锁；
/// 不能依赖 <c>MediaPlayer</c> 自带的 loop 标志。
/// </para>
/// <para>
/// 本机实测：<c>libvlc</c> 的 <c>libvlc_new</c> 在缺少 VLC 插件包时返回 null
/// （Arch 上 <c>libvlc</c> 与 <c>vlc</c> 是两个包）。因此<b>必须</b>软探测并回退。
/// </para>
/// </remarks>
public sealed class BackgroundService : IDisposable
{
    private readonly IXdgPaths _paths;

    private LibVLCSharp.Shared.LibVLC? _libVlc;

    private LibVLCSharp.Shared.MediaPlayer? _player;

    private bool _probed;

    private bool _available;


    public BackgroundService(IXdgPaths paths)
    {
        _paths = paths;
    }


    /// <summary>VLC 是否可用（探测结果缓存）。</summary>
    public bool IsVlcAvailable
    {
        get
        {
            if (!_probed)
            {
                _probed = true;
                _available = ProbeVlc(out _);
            }

            return _available;
        }
    }


    public string? ProbeError { get; private set; }


    /// <summary>探测 <c>libvlc</c> 是否可用，以及失败原因（用于 UI 提示安装命令）。</summary>
    public static bool ProbeVlc(out string? error)
    {
        error = null;
        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            using var lib = new LibVLCSharp.Shared.LibVLC("--no-audio", "--quiet");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }


    /// <summary>当前播放器（可能为 null）。</summary>
    public LibVLCSharp.Shared.MediaPlayer? Player => _player;


    /// <summary>
    /// 为给定条目创建播放器。失败返回 <c>null</c>（调用方回退到静态图）。
    /// </summary>
    public LibVLCSharp.Shared.MediaPlayer? EnsurePlayer(Action replayRequested)
    {
        if (!IsVlcAvailable)
        {
            return null;
        }

        if (_player is not null)
        {
            return _player;
        }

        try
        {
            _libVlc = new LibVLCSharp.Shared.LibVLC("--no-audio", "--quiet", "--no-video-title-show");
            _player = new LibVLCSharp.Shared.MediaPlayer(_libVlc)
            {
                Mute = true,
                EnableHardwareDecoding = true,
            };
            _player.EndReached += (_, _) =>
            {
                // 必须回 UI 线程再重播
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        replayRequested();
                        _player?.Play();
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                });
            };
            return _player;
        }
        catch (Exception ex)
        {
            ProbeError = ex.Message;
            _available = false;
            return null;
        }
    }


    /// <summary>播放一个本地视频文件（循环）。</summary>
    public void PlayLoop(string videoPath)
    {
        var player = EnsurePlayer(() => { });
        if (player is null || _libVlc is null)
        {
            return;
        }

        try
        {
            player.Play(new LibVLCSharp.Shared.Media(_libVlc, new Uri(videoPath)));
        }
        catch (Exception ex)
        {
            ProbeError = ex.Message;
        }
    }


    /// <summary>游戏 Running 时暂停（省 GPU / 解码器）。</summary>
    public void Pause()
    {
        try
        {
            _player?.Pause();
        }
        catch (Exception)
        {
            // ignore
        }
    }


    public void Resume()
    {
        try
        {
            _player?.Play();
        }
        catch (Exception)
        {
            // ignore
        }
    }


    /// <summary>背景缓存目录。</summary>
    public string CacheDirectory => _paths.BackgroundDirectory;


    /// <summary>
    /// 扫描缓存目录，列出可用的背景（视频优先）。
    /// </summary>
    public IReadOnlyList<BackgroundItem> Discover()
    {
        var result = new List<BackgroundItem>();
        try
        {
            if (Directory.Exists(CacheDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(CacheDirectory).OrderBy(x => x))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    var title = Path.GetFileNameWithoutExtension(file);
                    result.Add(ext switch
                    {
                        ".mp4" or ".webm" or ".mkv" => new BackgroundItem(file, null, title),
                        ".jpg" or ".jpeg" or ".png" or ".webp" => new BackgroundItem(null, file, title),
                        _ => new BackgroundItem(null, null, title),
                    });
                }
            }
        }
        catch (Exception)
        {
            // ignore
        }

        return result.Where(x => x.IsVideo || x.ImagePath is not null).ToList();
    }


    public void Dispose()
    {
        try
        {
            _player?.Stop();
            _player?.Dispose();
            _libVlc?.Dispose();
        }
        catch (Exception)
        {
            // ignore
        }

        _player = null;
        _libVlc = null;
    }
}


/// <summary>
/// 日志：<c>Serilog</c> 文件 sink，按天滚动，单文件上限 8 MiB，保留 14 个。
/// </summary>
/// <remarks>
/// 与 <c>docs/02-ARCHITECTURE.md</c> §八 一致：位置
/// <c>$XDG_CACHE_HOME/lunarward/logs/lunarward-&lt;date&gt;.log</c>。
/// 日志写入失败<b>绝不影响主流程</b>——所有方法都吞异常。
/// </remarks>
public sealed class LogService : IDisposable
{
    /// <summary>单个日志文件上限（与 <c>GAME_LOG_FILE_LIMIT</c> 同量级）。</summary>
    public const long FileSizeLimitBytes = 8 * 1024 * 1024;


    private readonly string _directory;

    private readonly Serilog.Core.Logger _logger;


    public LogService(IXdgPaths paths)
    {
        _directory = paths.LogDirectory;
        Directory.CreateDirectory(_directory);

        _logger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Join(_directory, "lunarward-.log"),  // Serilog 在 "-" 与扩展名之间插入日期
                rollingInterval: Serilog.RollingInterval.Day,
                fileSizeLimitBytes: FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }


    /// <summary>当前滚动文件路径（供 UI 展示/打开）。</summary>
    public string LogFile => Path.Join(_directory, $"lunarward-{DateTime.Now:yyyy-MM-dd}.log");


    public void Info(string message) => Write(() => _logger.Information("{Message}", message));

    public void Warn(string message) => Write(() => _logger.Warning("{Message}", message));

    public void Error(string message, Exception? ex = null)
        => Write(() => _logger.Error(ex, "{Message}", message));


    private static void Write(Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            // 日志失败绝不影响主流程
        }
    }


    public void Dispose()
    {
        try
        {
            _logger.Dispose();
        }
        catch (Exception)
        {
            // ignore
        }
    }
}
