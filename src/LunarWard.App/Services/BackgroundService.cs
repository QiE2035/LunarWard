using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
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

    private static readonly TimeSpan OfficialCacheTtl = TimeSpan.FromMinutes(10);

    private readonly HttpClient _download = new()
    {
        // 官方背景视频是整文件下载（十几 MB 起），不能沿用 15s 的接口超时。
        Timeout = TimeSpan.FromMinutes(5),
    };

    private readonly object _officialLock = new();
    private readonly Dictionary<string, (IReadOnlyList<BackgroundEntry> Entries, DateTimeOffset At)> _officialCache = [];

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
        // 只缓存「官方接口 + 海报」这一段：本地背景目录随时会被拖入/删除文件，必须每次现扫。
        var official = GetOfficialBackgrounds(launcherId, gameBiz, posterUrl, cancellationToken);
        var files = DiscoverCacheFiles();
        return BackgroundCatalog.Merge(await official.ConfigureAwait(false), posterUrl, files);
    }


    /// <summary>
    /// 官方背景（10 分钟内存缓存）。切换区服 / 页面重建时会重复问一次，
    /// 上游 <c>HoYoPlayService</c> 同样缓存 <c>GetGameBackgroundsAsync</c> 的结果。
    /// </summary>
    private Task<IReadOnlyList<BackgroundEntry>> GetOfficialBackgrounds(
        string launcherId,
        string gameBiz,
        string? posterUrl,
        CancellationToken cancellationToken)
    {
        var key = $"{launcherId}|{gameBiz}|{posterUrl}";
        lock (_officialLock)
        {
            if (_officialCache.TryGetValue(key, out var hit)
                && DateTimeOffset.UtcNow - hit.At < OfficialCacheTtl)
            {
                return Task.FromResult(hit.Entries);
            }
        }

        return FetchOfficialBackgroundsAsync(key, launcherId, gameBiz, posterUrl, cancellationToken);
    }


    private async Task<IReadOnlyList<BackgroundEntry>> FetchOfficialBackgroundsAsync(
        string key,
        string launcherId,
        string gameBiz,
        string? posterUrl,
        CancellationToken cancellationToken)
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 离线/接口改动：退化为海报 + 本地背景
            ProbeError ??= ex.Message;
        }

        IReadOnlyList<BackgroundEntry> entries = posterUrl is { Length: > 0 }
            ? BackgroundCatalog.Merge(official, posterUrl, [])
            : official;

        lock (_officialLock)
        {
            _officialCache[key] = (entries, DateTimeOffset.UtcNow);
        }

        return entries;
    }


    private IReadOnlyList<BackgroundEntry> DiscoverCacheFiles()
    {
        try
        {
            if (Directory.Exists(CacheDirectory))
            {
                return BackgroundCatalog.FromFiles(Directory.EnumerateFiles(CacheDirectory).OrderBy(x => x));
            }
        }
        catch (Exception)
        {
            // 目录不可读就当成没有本地背景
        }

        return [];
    }


    /// <summary>官方背景视频的本地缓存目录（上游是 <c>%CacheFolder%/bg</c>）。</summary>
    public string BackgroundVideoCacheDirectory
    {
        get
        {
            var parent = Path.GetDirectoryName(CacheDirectory.TrimEnd(Path.DirectorySeparatorChar));
            return Path.Join(string.IsNullOrEmpty(parent) ? CacheDirectory : parent, "bg");
        }
    }


    /// <summary>
    /// 把官方背景视频下到本地再交给播放器：上游 <c>BackgroundService.GetGameBackgroundsAsync</c>
    /// 就是这么做的（下载一次、之后本地循环），避免每圈「重新拉一遍」的带宽和卡顿。
    /// </summary>
    /// <returns>本地文件路径；下载失败返回 <c>null</c>（调用方回退为直接流播）。</returns>
    public async Task<string?> EnsureVideoCachedAsync(string source, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return File.Exists(source) ? source : null;
        }

        var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (extension is not (".mp4" or ".webm" or ".mkv"))
        {
            extension = ".mp4";
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)))[..16];
        var directory = BackgroundVideoCacheDirectory;
        var target = Path.Join(directory, hash + extension);
        if (File.Exists(target) && new FileInfo(target).Length > 0)
        {
            return target;
        }

        try
        {
            Directory.CreateDirectory(directory);
            var temporary = target + ".part";
            using (var response = await _download.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using var file = File.Create(temporary);
                await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, target, overwrite: true);
            PruneVideoCache(directory);
            return target;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ProbeError ??= ex.Message;
            return null;
        }
    }


    /// <summary>缓存上限 512 MiB，按最后写入时间淘汰。</summary>
    private static void PruneVideoCache(string directory)
    {
        try
        {
            const long limit = 512L * 1024 * 1024;
            var files = new DirectoryInfo(directory).EnumerateFiles()
                .Where(x => x.Name.EndsWith(".part", StringComparison.Ordinal) is false)
                .OrderByDescending(x => x.LastWriteTimeUtc)
                .ToList();
            var total = 0L;
            foreach (var file in files)
            {
                total += file.Length;
                if (total > limit)
                {
                    file.Delete();
                }
            }
        }
        catch (Exception)
        {
            // 清理失败不影响播放
        }
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
        _download.Dispose();
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
