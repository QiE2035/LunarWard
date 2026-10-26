using System.Collections.Concurrent;
using System.Net.Http;
using Avalonia.Media.Imaging;
using LunarWard.Services;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Platform;

namespace LunarWard.Services;

/// <summary>
/// Starward 版 UI 的运行时素材（游戏图标 / 缩略图 / 背景 / 轮播图 / 资讯）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 不在仓库里打包游戏素材，而是启动后从 HoYoPlay 的 <c>getGames</c> /
/// <c>getGameBackgrounds</c> / <c>getGameContent</c> 拉取（见
/// <c>Starward/Features/HoYoPlay/HoYoPlayService.cs</c>）。这里做同样的三件事，
/// 并把图片缓存到 <c>~/.cache/lunarward/starward</c>，离线时从缓存加载。
/// </para>
/// <para>
/// 网络失败不是错误：图标缺失时 UI 退化为纯色占位块，公告卡整体隐藏——
/// 与 Starward 的离线行为一致。
/// </para>
/// </remarks>
public sealed class StarwardAssets : IDisposable
{
    private static readonly string[] PreferredLanguages = ["zh-cn", "zh-CN", "en-us"];

    private readonly IXdgPaths _paths;

    private readonly LogService _log;

    private readonly HttpClient _http;

    private readonly ConcurrentDictionary<string, Bitmap?> _bitmapCache = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _bitmapTasks = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, GameInfo> _gameInfos = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>每个 launcher 的 <c>getGames</c> 只请求一次（失败才移除，下次重试）。</summary>
    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<GameInfo>>>> _launcherGames = new(StringComparer.Ordinal);

    /// <summary>按 <c>launcherId|gameId|lang</c> 缓存的资讯；不同游戏各自一份，绝不共用。</summary>
    private readonly ConcurrentDictionary<string, GameContent> _contentCache = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _contentFetchedAt = new(StringComparer.Ordinal);

    /// <summary>同一 key 并发请求只发一次 HTTP（<see cref="Lazy{T}"/> 保证只有一个任务真正启动）。</summary>
    private readonly ConcurrentDictionary<string, Lazy<Task<GameContent?>>> _contentTasks = new(StringComparer.Ordinal);

    /// <summary>同一个 URL 的下载只跑一次（页面与外壳会并发请求同一张图）。</summary>
    private readonly ConcurrentDictionary<string, Task<string?>> _downloadTasks = new(StringComparer.Ordinal);

    private static readonly TimeSpan ContentLifetime = TimeSpan.FromMinutes(30);

    private readonly HoYoPlayClient _client;

    private readonly string _cacheRoot;


    public StarwardAssets(IXdgPaths paths, LogService log)
        : this(paths, log, null)
    {
    }


    /// <param name="handler">测试用 HTTP handler；为 null 时使用默认的 <see cref="HttpClientHandler"/>。</param>
    public StarwardAssets(IXdgPaths paths, LogService log, HttpMessageHandler? handler)
    {
        _paths = paths;
        _log = log;
        _cacheRoot = Path.Join(paths.CacheHome, "starward");
        Directory.CreateDirectory(_cacheRoot);
        _http = handler is null
            ? new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
            {
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
                Timeout = TimeSpan.FromSeconds(20),
            }
            : new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        _client = new HoYoPlayClient(_http);
    }


    /// <summary>当前为首选语言拉到的游戏素材。</summary>
    public GameArt? Art { get; private set; }


    /// <summary>当前游戏 biz（如 <c>hk4e_cn</c>）。</summary>
    public string? CurrentGameBiz { get; private set; }


    /// <summary>
    /// 拉取当前游戏的素材（图标、缩略图、Logo、背景）并记为 <see cref="Art"/>。
    /// </summary>
    /// <remarks>
    /// Starward 每个游戏用自己的 launcher id（国服 / 国际服 / B 站各一条），
    /// 所以游戏选择层要看到全部游戏的卡片就必须把用到的 launcher id 都拉一遍。
    /// 启动器页的资讯不在这里加载，见 <see cref="GetContentAsync"/>。
    /// </remarks>
    public async Task LoadAsync(string launcherId, string gameBiz, CancellationToken cancellationToken = default)
    {
        try
        {
            Art = await GetArtAsync(launcherId, gameBiz, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warn($"素材加载失败（{gameBiz}）：{ex.Message}");
            Art = null;
        }
        finally
        {
            CurrentGameBiz = gameBiz;
        }
    }


    /// <summary>
    /// 取某个游戏的素材路径。找不到该游戏时返回 null，不会回落到别的游戏。
    /// </summary>
    public async Task<GameArt?> GetArtAsync(string launcherId, string gameBiz, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(launcherId) || string.IsNullOrWhiteSpace(gameBiz))
        {
            return null;
        }

        var language = PickLanguage();
        await EnsureGameInfosAsync(launcherId, language).ConfigureAwait(false);
        if (!_gameInfos.TryGetValue(gameBiz, out var target))
        {
            _log.Warn($"素材：{launcherId} 的 getGames 里没有 {gameBiz}");
            return null;
        }

        var display = target.Display;
        return new GameArt(
            gameBiz,
            await CacheAsync(display?.Icon?.Url, "icon", cancellationToken).ConfigureAwait(false),
            await CacheAsync(display?.Thumbnail?.Url, "thumb", cancellationToken).ConfigureAwait(false),
            await CacheAsync(display?.Logo?.Url, "logo", cancellationToken).ConfigureAwait(false),
            await CacheAsync(display?.Background?.Url, "background", cancellationToken).ConfigureAwait(false));
    }


    /// <summary>
    /// 只刷新资讯/轮播图：先让该游戏的缓存失效，再重新拉取（Starward 定时 1 小时刷新一次内容）。
    /// </summary>
    public async Task RefreshContentAsync(string launcherId, string gameBiz, CancellationToken cancellationToken = default)
    {
        try
        {
            var language = PickLanguage();
            if (_gameInfos.TryGetValue(gameBiz, out var info) && info.Id is { Length: > 0 } gameId)
            {
                var key = ContentKey(launcherId, gameId, language);
                _contentCache.TryRemove(key, out _);
                _contentFetchedAt.TryRemove(key, out _);
                _contentTasks.TryRemove(key, out _);
            }

            await GetContentAsync(launcherId, gameBiz, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warn($"资讯刷新失败（{gameBiz}）：{ex.Message}");
        }
    }


    /// <summary>
    /// 取某个游戏的资讯（轮播图 + 活动/公告/资讯三组）。
    /// </summary>
    /// <remarks>
    /// 纯查询 API：没有共享的「当前 Content」字段，调用方拿到什么就展示什么，
    /// 因此并发请求两个游戏也不会互相覆盖。同一 <paramref name="gameBiz"/> 的并发
    /// 请求会共用一次 HTTP；30 分钟内的重复请求直接返回该游戏自己的缓存。
    /// </remarks>
    public async Task<GameContent?> GetContentAsync(string launcherId, string gameBiz, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(launcherId) || string.IsNullOrWhiteSpace(gameBiz))
        {
            return null;
        }

        try
        {
            var language = PickLanguage();
            await EnsureGameInfosAsync(launcherId, language).ConfigureAwait(false);
            if (!_gameInfos.TryGetValue(gameBiz, out var info) || info.Id is not { Length: > 0 } gameId)
            {
                _log.Warn($"资讯：{launcherId} 的 getGames 里没有 {gameBiz}");
                return null;
            }

            var key = ContentKey(launcherId, gameId, language);
            if (_contentCache.TryGetValue(key, out var cached) &&
                _contentFetchedAt.TryGetValue(key, out var fetchedAt))
            {
                if (DateTimeOffset.Now - fetchedAt < ContentLifetime)
                {
                    return cached;
                }

                // 过期：丢掉旧的单飞任务，否则重新拉取时还会拿到第一次的结果。
                _contentTasks.TryRemove(key, out _);
            }

            var lazy = _contentTasks.GetOrAdd(key, _ =>
                new Lazy<Task<GameContent?>>(() => FetchContentAsync(key, launcherId, language, gameBiz, gameId)));
            return await lazy.Value.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warn($"getGameContent 失败（{gameBiz}）：{ex.Message}");
            return null;
        }
    }


    private async Task<GameContent?> FetchContentAsync(string key, string launcherId, string language, string gameBiz, string gameId)
    {
        try
        {
            var content = await _client.GetGameContentAsync(
                launcherId, language, new GameId { Id = gameId, GameBiz = gameBiz }).ConfigureAwait(false);

            // 万一接口返回了别的游戏的内容，宁可当作失败也不能串给当前页面。
            var returnedId = content?.GameId?.Id;
            if (content is null ||
                (returnedId is { Length: > 0 } && !string.Equals(returnedId, gameId, StringComparison.OrdinalIgnoreCase)))
            {
                _log.Warn($"getGameContent 返回了不匹配的游戏（期望 {gameId}，实际 {returnedId ?? "null"}）");
                _contentTasks.TryRemove(key, out _);
                return null;
            }

            _contentCache[key] = content;
            _contentFetchedAt[key] = DateTimeOffset.Now;
            return content;
        }
        catch (Exception ex)
        {
            _log.Warn($"getGameContent 失败（{gameBiz}）：{ex.Message}");
            _contentTasks.TryRemove(key, out _);
            return null;
        }
    }


    private static string ContentKey(string launcherId, string gameId, string language)
        => $"{launcherId}|{gameId}|{language}";


    /// <summary>
    /// 确保某个 launcher 的 <c>getGames</c> 结果已就绪；并发调用共用一个 HTTP 请求。
    /// </summary>
    private Task<IReadOnlyList<GameInfo>> EnsureGameInfosAsync(string launcherId, string language)
    {
        var key = $"{launcherId}|{language}";
        var lazy = _launcherGames.GetOrAdd(key, _ =>
            new Lazy<Task<IReadOnlyList<GameInfo>>>(() => FetchGameInfosAsync(key, launcherId, language)));
        return lazy.Value;
    }


    private async Task<IReadOnlyList<GameInfo>> FetchGameInfosAsync(string key, string launcherId, string language)
    {
        try
        {
            var infos = await _client.GetGameInfoAsync(launcherId, language).ConfigureAwait(false);
            foreach (var info in infos)
            {
                if (info is not null && info.GameBiz.Value is { Length: > 0 } biz)
                {
                    _gameInfos[biz] = info;
                }
            }

            return infos;
        }
        catch (Exception ex)
        {
            _log.Warn($"素材：launcher {launcherId} 拉取失败（{ex.Message}）");
            // 失败不缓存，下次调用还能重试
            _launcherGames.TryRemove(key, out _);
            return [];
        }
    }


    /// <summary>
    /// 把游戏选择层要用到的 launcher id 全部拉一遍（国服 + 国际服 + B 站），
    /// 这样未安装的游戏也有图标/缩略图。已拉过的 launcher id 不会重复请求。
    /// </summary>
    public async Task LoadAllLaunchersAsync(IEnumerable<string> launcherIds, CancellationToken cancellationToken = default)
    {
        var language = PickLanguage();
        foreach (var launcherId in launcherIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureGameInfosAsync(launcherId, language).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// 游戏选择层用的卡片素材：只下载缩略图与 Logo（不碰几 MB 的大背景图）。
    /// </summary>
    public async Task<(Bitmap? Thumbnail, Bitmap? Logo)?> CardArtAsync(string gameBiz, CancellationToken cancellationToken = default)
    {
        if (!_gameInfos.TryGetValue(gameBiz, out var info))
        {
            return null;
        }

        var thumbnail = await BitmapAsync(info.Display?.Thumbnail?.Url, cancellationToken).ConfigureAwait(false);
        var logo = await BitmapAsync(info.Display?.Logo?.Url, cancellationToken).ConfigureAwait(false);
        return (thumbnail, logo);
    }


    /// <summary>取当前游戏（或指定 biz）的素材。</summary>
    public GameArt? For(string? gameBiz = null)
    {
        if (gameBiz is null || string.Equals(gameBiz, CurrentGameBiz, StringComparison.OrdinalIgnoreCase))
        {
            return Art;
        }

        return _gameInfos.TryGetValue(gameBiz, out var info)
            ? new GameArt(gameBiz, info.Display?.Icon?.Url, info.Display?.Thumbnail?.Url, info.Display?.Logo?.Url, info.Display?.Background?.Url)
            : null;
    }


    /// <summary>所有已知游戏的素材（游戏选择层用），按 biz 去重。</summary>
    public IReadOnlyList<GameArt> AllArt => _gameInfos.Values
        .Where(x => x.GameBiz.Value is { Length: > 0 })
        .Select(x => new GameArt(x.GameBiz.Value, x.Display?.Icon?.Url, x.Display?.Thumbnail?.Url, x.Display?.Logo?.Url, x.Display?.Background?.Url))
        .GroupBy(x => x.GameBiz, StringComparer.OrdinalIgnoreCase)
        .Select(x => x.First())
        .ToList();


    /// <summary>把 URL 下载到缓存；已是本地路径就直接返回。失败返回 null。</summary>
    /// <remarks>并发调用同一个 URL 时共用一次下载（否则两个 <c>.tmp</c> 会互相踩）。</remarks>
    public Task<string?> CacheAsync(string? url, string kind, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult<string?>(null);
        }

        return _downloadTasks.GetOrAdd(url, key => DownloadAsync(key, kind, cancellationToken));
    }


    private async Task<string?> DownloadAsync(string url, string kind, CancellationToken cancellationToken)
    {
        try
        {
            return await DownloadCoreAsync(url, kind, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _downloadTasks.TryRemove(url, out _);
        }
    }


    private async Task<string?> DownloadCoreAsync(string url, string kind, CancellationToken cancellationToken)
    {

        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(url) ? url : null;
        }

        try
        {
            var extension = Path.GetExtension(new Uri(url).AbsolutePath);
            if (string.IsNullOrEmpty(extension) || extension.Length > 5)
            {
                extension = ".png";
            }

            // 注意：扩展名来自 URL，但服务端有时返回 WebP/AVIF 而 URL 没有后缀。
            // 解码交给 Avalonia 的 Bitmap（按魔数嗅探），扩展名只用于缓存文件名。

            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(url)))[..16];
            // 每类素材一个前缀，便于人工排查缓存目录
            var path = Path.Join(_cacheRoot, $"{kind}-{hash}{extension}");
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return path;
            }

            var bytes = await _http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
            var temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
            return path;
        }
        catch (Exception ex)
        {
            _log.Warn($"图片下载失败（{kind}）：{ex.Message}");
            return null;
        }
    }


    /// <summary>按路径/URL 取位图，带内存缓存（同一张图只解码一次）。</summary>
    public Task<Bitmap?> BitmapAsync(string? pathOrUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return Task.FromResult<Bitmap?>(null);
        }

        if (_bitmapCache.TryGetValue(pathOrUrl, out var cached))
        {
            return Task.FromResult(cached);
        }

        return _bitmapTasks.GetOrAdd(pathOrUrl, async key =>
        {
            var bitmap = await LoadBitmapAsync(key, cancellationToken).ConfigureAwait(false);
            _bitmapCache[key] = bitmap;
            _bitmapTasks.TryRemove(key, out _);
            return bitmap;
        });
    }


    private async Task<Bitmap?> LoadBitmapAsync(string pathOrUrl, CancellationToken cancellationToken)
    {
        try
        {
            var path = pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? await CacheAsync(pathOrUrl, "image", cancellationToken).ConfigureAwait(false)
                : pathOrUrl;

            if (path is null || !File.Exists(path))
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            _log.Warn($"图片解码失败：{ex.Message}");
            return null;
        }
    }


    private static string PickLanguage()
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        return PreferredLanguages.FirstOrDefault(x => x.Equals(culture, StringComparison.OrdinalIgnoreCase))
               ?? (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-cn" : "en-us");
    }


    public void Dispose()
    {
        _http.Dispose();
        foreach (var bitmap in _bitmapCache.Values)
        {
            bitmap?.Dispose();
        }
    }
}


/// <summary>一个游戏的素材路径集合（可能部分为 null：离线/接口缺失）。</summary>
public sealed record GameArt(string GameBiz, string? Icon, string? Thumbnail, string? Logo, string? Background);
