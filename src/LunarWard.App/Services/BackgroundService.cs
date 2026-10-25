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
/// 动态背景：视频交给 <see cref="VideoBackgroundPlayer"/>（<c>ffmpeg</c> 子进程），
/// 不可用时静默回退静态图。
/// </summary>
/// <remarks>
/// <para>
/// 见 <c>docs/04-FEATURES.md</c> §5.4。原来用 <c>LibVLCSharp</c>，但
/// <c>VideoView</c> 依赖原生 X11 子窗口（Wayland 下背景全黑），而 Arch 系发行版把 VLC
/// 拆成 <c>vlc-plugin-*</c> 多个包，缺 <c>vlc-plugin-ffmpeg</c> 时连 H.264 都解不了。
/// 改用 <c>ffmpeg</c> 子进程后与合成器、VLC 打包方式都解耦。
/// </para>
/// <para>
/// ffmpeg 是软依赖：探测不到就只显示静态背景，不抛异常。
/// </para>
/// </remarks>
public sealed class BackgroundService : IDisposable
{
    private readonly IXdgPaths _paths;

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    private static readonly Lazy<bool> Ffmpeg = new(ProbeFfmpeg);

    private VideoBackgroundPlayer? _activePlayer;


    public BackgroundService(IXdgPaths paths)
    {
        _paths = paths;
    }


    /// <summary>视频背景是否可用（探测结果缓存）。</summary>
    public bool IsVideoAvailable => Ffmpeg.Value;


    /// <summary>视频背景不可用时的原因（用于 UI 提示安装命令）。</summary>
    public string? ProbeError { get; private set; }


    /// <summary>探测 <c>ffmpeg</c> 是否在 <c>PATH</c> 上。</summary>
    public static bool IsFfmpegAvailable => Ffmpeg.Value;


    private static bool ProbeFfmpeg()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ffmpeg", "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return false;
            }

            process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }


    /// <summary>登记当前活跃的播放器（由外壳创建），供暂停/停止用。</summary>
    public void AttachPlayer(VideoBackgroundPlayer? player)
    {
        _activePlayer = player;
        if (player is null)
        {
            ProbeError = null;
        }
        else if (!Ffmpeg.Value)
        {
            ProbeError = "未找到 ffmpeg";
        }
    }


    /// <summary>暂停并释放视频背景（切回静态图时用）。</summary>
    public void Stop() => _activePlayer?.Stop();


    /// <summary>
    /// 本地路径 → <c>file://</c>，远端地址原样返回。
    /// </summary>
    /// <remarks>
    /// <c>new Uri("/tmp/a.mp4")</c> 在 .NET 里会抛「URI 格式无法确定」，
    /// 必须先用 <see cref="Uri.TryCreate(string, UriKind, out Uri)"/> 判断绝对地址。
    /// </remarks>
    private static Uri ToMediaUri(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        return new Uri(Path.GetFullPath(path));
    }


    /// <summary>游戏 Running 时暂停（省 CPU：SIGSTOP）。</summary>
    public void Pause() => _activePlayer?.Pause();


    public void Resume() => _activePlayer?.Resume();


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


    /// <summary>
    /// 加载可切换背景：官方接口（图 + 官方动态视频）→ 版本海报 → 本地背景目录。
    /// </summary>
    /// <param name="launcherId">启动器 id（<c>HoYoPlayClient</c> 用），如 <c>jGHFHHS</c>。</param>
    /// <param name="gameBiz">游戏区服标识，如 <c>hk4e_cn</c>。</param>
    /// <param name="posterUrl">游戏素材里的版本海报（<c>GameInfo.Display.Background</c>），接口没有时兜底。</param>
    public async Task<IReadOnlyList<BackgroundEntry>> LoadCatalogAsync(
        string launcherId,
        string gameBiz,
        string? posterUrl,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BackgroundEntry> official = [];
        try
        {
            if (Core.HoYoPlay.GameId.FromGameBiz(new Core.GameBiz(gameBiz)) is { } gameId)
            {
                var client = new Core.HoYoPlay.HoYoPlayClient(_http);
                var info = await client.GetGameBackgroundAsync(launcherId, "zh-cn", gameId, cancellationToken)
                    .ConfigureAwait(false);
                official = BackgroundCatalog.FromApi(info);
            }
        }
        catch (Exception ex)
        {
            // 离线/接口改动：退化为海报 + 本地背景
            ProbeError ??= ex.Message;
        }

        IReadOnlyList<string> files = [];
        try
        {
            if (Directory.Exists(CacheDirectory))
            {
                files = Directory.EnumerateFiles(CacheDirectory).OrderBy(x => x).ToList();
            }
        }
        catch (Exception)
        {
            // 目录不可读就当成没有本地背景
        }

        return BackgroundCatalog.Merge(official, posterUrl, BackgroundCatalog.FromFiles(files));
    }


    public void Dispose()
    {
        try
        {
            _activePlayer?.Stop();
            _activePlayer = null;
        }
        catch (Exception)
        {
            // ignore
        }

        _http.Dispose();
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
