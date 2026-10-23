using System.Collections.Concurrent;
using System.Net.Http;
using Avalonia.Media.Imaging;
using LunarWard.App.Services;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Platform;

namespace LunarWard.App.Services;

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

    private readonly ConcurrentDictionary<string, DateTimeOffset> _contentFetchedAt = new(StringComparer.Ordinal);

    /// <summary>同一个 URL 的下载只跑一次（页面与外壳会并发请求同一张图）。</summary>
    private readonly ConcurrentDictionary<string, Task<string?>> _downloadTasks = new(StringComparer.Ordinal);

    private readonly HoYoPlayClient _client;

    private readonly string _cacheRoot;

    private string _language = "zh-cn";


    public StarwardAssets(IXdgPaths paths, LogService log)
    {
        _paths = paths;
        _log = log;
        _cacheRoot = Path.Join(paths.CacheHome, "starward");
        Directory.CreateDirectory(_cacheRoot);
        _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            Timeout = TimeSpan.FromSeconds(20),
        };
        _client = new HoYoPlayClient(_http);
    }


    /// <summary>当前为首选语言拉到的游戏素材。</summary>
    public GameArt? Art { get; private set; }


    /// <summary>当前游戏 biz（如 <c>hk4e_cn</c>）。</summary>
    public string? CurrentGameBiz { get; private set; }


    /// <summary>Starward 的「活动 / 公告 / 资讯」三组资讯 + 轮播图。</summary>
    public GameContent? Content { get; private set; }


    /// <summary>已经拉过 <c>getGames</c> 的 launcher id（避免重复请求）。</summary>
    private readonly ConcurrentDictionary<string, bool> _loadedLaunchers = new(StringComparer.Ordinal);


    /// <summary>
    /// 拉取某个游戏组合的全部素材（图标、背景、轮播图、资讯）。
    /// </summary>
    /// <remarks>
    /// Starward 每个游戏用自己的 launcher id（国服 / 国际服 / B 站各一条），
    /// 所以游戏选择层要看到全部游戏的卡片就必须把用到的 launcher id 都拉一遍。
    /// </remarks>
    public async Task LoadAsync(string launcherId, string gameBiz, CancellationToken cancellationToken = default)
    {
        try
        {
            _language = PickLanguage();
            var infos = await _client.GetGameInfoAsync(launcherId, _language, cancellationToken).ConfigureAwait(false);
            _loadedLaunchers[launcherId] = true;
            foreach (var info in infos)
            {
                if (info is not null && info.GameBiz.Value is { Length: > 0 } biz)
                {
                    _gameInfos[biz] = info;
                }
            }

            if (!_gameInfos.TryGetValue(gameBiz, out var target))
            {
                _log.Warn($"素材：{launcherId} 的 getGames 里没有 {gameBiz}");
                CurrentGameBiz = gameBiz;
                Art = null;
                Content = null;
                return;
            }

            CurrentGameBiz = gameBiz;
            var display = target.Display;
            Art = new GameArt(
                gameBiz,
                await CacheAsync(display?.Icon?.Url, "icon", cancellationToken).ConfigureAwait(false),
                await CacheAsync(display?.Thumbnail?.Url, "thumb", cancellationToken).ConfigureAwait(false),
                await CacheAsync(display?.Logo?.Url, "logo", cancellationToken).ConfigureAwait(false),
                await CacheAsync(display?.Background?.Url, "background", cancellationToken).ConfigureAwait(false));

            await LoadContentAsync(launcherId, gameBiz, target.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warn($"素材加载失败（{gameBiz}）：{ex.Message}");
            CurrentGameBiz = gameBiz;
        }
    }


    /// <summary>只刷新资讯/轮播图（Starward 定时 1 小时刷新一次内容）。</summary>
    public async Task RefreshContentAsync(string launcherId, string gameBiz, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_gameInfos.TryGetValue(gameBiz, out var info))
            {
                await LoadAsync(launcherId, gameBiz, cancellationToken).ConfigureAwait(false);
                return;
            }

            await LoadContentAsync(launcherId, gameBiz, info.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warn($"资讯刷新失败（{gameBiz}）：{ex.Message}");
        }
    }


    private async Task LoadContentAsync(string launcherId, string gameBiz, string? gameId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gameId))
        {
            return;
        }

        var key = $"{launcherId}|{gameId}|{_language}";
        if (_contentFetchedAt.TryGetValue(key, out var at) && DateTimeOffset.Now - at < TimeSpan.FromMinutes(30))
        {
            return;
        }

        try
        {
            Content = await _client.GetGameContentAsync(
                launcherId, _language, new GameId { Id = gameId, GameBiz = gameBiz }, cancellationToken).ConfigureAwait(false);
            _contentFetchedAt[key] = DateTimeOffset.Now;
        }
        catch (Exception ex)
        {
            _log.Warn($"getGameContent 失败（{gameBiz}）：{ex.Message}");
        }
    }


    /// <summary>
    /// 把游戏选择层要用到的 launcher id 全部拉一遍（国服 + 国际服 + B 站），
    /// 这样未安装的游戏也有图标/缩略图。已拉过的 launcher id 不会重复请求。
    /// </summary>
    public async Task LoadAllLaunchersAsync(IEnumerable<string> launcherIds, CancellationToken cancellationToken = default)
    {
        foreach (var launcherId in launcherIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            if (_loadedLaunchers.ContainsKey(launcherId))
            {
                continue;
            }

            try
            {
                _language = PickLanguage();
                var infos = await _client.GetGameInfoAsync(launcherId, _language, cancellationToken).ConfigureAwait(false);
                _loadedLaunchers[launcherId] = true;
                foreach (var info in infos)
                {
                    if (info is not null && info.GameBiz.Value is { Length: > 0 } biz)
                    {
                        _gameInfos[biz] = info;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"素材：launcher {launcherId} 拉取失败（{ex.Message}）");
            }
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
