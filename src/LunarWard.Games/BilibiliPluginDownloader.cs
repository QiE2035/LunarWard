using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using LunarWard.Platform;

namespace LunarWard.Games;

/// <summary>
/// 下载并安装 B 站渠道的 <c>PCGameSDK.dll</c>。
/// </summary>
/// <remarks>
/// <para>
/// 行为照搬 <c>anime-launcher-sdk/src/games/genshin/env_emulation.rs</c>
/// （见 <c>docs/01-RESEARCH.md</c> §3.3 与 <c>docs/04-FEATURES.md</c> §2.6）。
/// </para>
/// <para><b>两个陷阱</b>：</para>
/// <list type="number">
/// <item>
/// 插件 CDN 对没有浏览器 UA 的请求返回 <b>403</b>，而且 <b>HEAD 请求也必须带</b>
/// （如果先探测大小）。所以这里用 <c>SendAsync</c> + 手工塞头，而不是
/// <c>HttpClient.GetAsync</c>（后者无法在任何版本上保证 HEAD 也带头）。
/// </item>
/// <item>
/// zip 条目名是 <b>GBK</b> 编码（<c>64位</c> 存成 <c>64\xce\xbb</c>），但可能同时带
/// Unicode 扩展字段。两种表示都以 <c>64</c> 开头，所以按"前缀 64 + 后缀 /PCGameSDK.dll"
/// 匹配，不依赖解码正确性。
/// </item>
/// </list>
/// <para>
/// 缓存到 <c>$XDG_CACHE_HOME/lunarward/bilibili/PCGameSDK5.1.0.zip</c>；
/// 下载到 <c>.part</c> 再 rename，解压到 <c>.dll.part</c> 再 rename——
/// 缓存与游戏目录里都不会出现半截文件。
/// </para>
/// </remarks>
public sealed class BilibiliPluginDownloader
{
    /// <summary>官方 B 站 PC 游戏 SDK 包地址。</summary>
    public const string PluginUri = "https://pkg.biligame.com/games/PCGameSDK5.1.0/051985/PCGameSDK5.1.0.zip";

    /// <summary>缓存里的包名。</summary>
    public const string ArchiveFileName = "PCGameSDK5.1.0.zip";

    /// <summary>插件文件名。</summary>
    public const string PluginFileName = "PCGameSDK.dll";

    /// <summary>zip 内部条目的后缀。</summary>
    public const string PluginEntrySuffix = "/PCGameSDK.dll";

    /// <summary>zip 内部条目的架构前缀（匹配 <c>64位</c> 的两种表示）。</summary>
    public const string PluginEntryArchPrefix = "64";

    /// <summary>CDN 没有浏览器 UA 会 403。</summary>
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    /// <summary>同样必须带上的 Referer。</summary>
    public const string Referer = "https://open.biligame.com/";


    private readonly HttpClient _http;

    private readonly string _cacheDirectory;

    private readonly bool _ownsHttpClient;


    /// <summary>用 <see cref="XdgPaths.Default"/> 的缓存目录构造。</summary>
    public BilibiliPluginDownloader(HttpClient? httpClient = null)
        : this(XdgPaths.Default, httpClient)
    {
    }


    public BilibiliPluginDownloader(IXdgPaths paths, HttpClient? httpClient = null)
        : this(paths?.BilibiliDirectory ?? throw new ArgumentNullException(nameof(paths)), httpClient)
    {
    }


    /// <param name="cacheDirectory">缓存目录（<c>…/lunarward/bilibili</c>）。</param>
    /// <param name="httpClient">可注入（测试用）；<c>null</c> 时内部创建一个带 handler 的实例。</param>
    public BilibiliPluginDownloader(string cacheDirectory, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        _cacheDirectory = cacheDirectory;
        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? CreateDefaultClient();
    }


    /// <summary>缓存目录。</summary>
    public string CacheDirectory => _cacheDirectory;

    /// <summary>缓存包路径。</summary>
    public string ArchivePath => Path.Join(_cacheDirectory, ArchiveFileName);


    /// <summary>
    /// 创建默认的 <see cref="HttpClient"/>。
    /// </summary>
    /// <remarks>
    /// 上游 <c>anime-game-core</c> 有一条 fork 分支的唯一目的就是给 <c>Downloader</c>
    /// 加自定义 header 支持；这里用 <c>DefaultRequestHeaders</c> 直接解决，
    /// 所以 <b>所有</b> 请求（含 HEAD）都会带头。
    /// </remarks>
    public static HttpClient CreateDefaultClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(30),
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(30),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Referrer = new Uri(Referer);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        return client;
    }


    // ─────────────────────────── 条目匹配（U11） ───────────────────────────

    /// <summary>
    /// 判断 zip 条目名是否是 64 位插件。
    /// </summary>
    /// <remarks>
    /// <c>name.EndsWith("/PCGameSDK.dll") &amp;&amp; name.Split('/').Any(p => p.StartsWith("64"))</c>。
    /// 纯函数，可直接用原始字节序列测试（U11）。
    /// </remarks>
    public static bool IsPluginEntry(string? entryName)
    {
        if (string.IsNullOrEmpty(entryName))
        {
            return false;
        }

        if (!entryName.EndsWith(PluginEntrySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return entryName.Split('/')
            .Any(part => part.StartsWith(PluginEntryArchPrefix, StringComparison.Ordinal));
    }


    /// <summary>在已打开的 zip 里找 64 位插件条目；找不到返回 <c>null</c>。</summary>
    public static ZipArchiveEntry? FindPluginEntry(ZipArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        foreach (var entry in archive.Entries)
        {
            if (IsPluginEntry(entry.FullName))
            {
                return entry;
            }
        }

        return null;
    }


    // ─────────────────────────── 下载 ───────────────────────────

    /// <summary>
    /// 确保缓存包存在，返回其路径。已缓存则直接返回（缓存文件只在完整时才存在）。
    /// </summary>
    public async Task<string> EnsureArchiveAsync(
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var archive = ArchivePath;
        if (File.Exists(archive) && new FileInfo(archive).Length > 0)
        {
            progress?.Report(1d);
            return archive;
        }

        Directory.CreateDirectory(_cacheDirectory);

        var part = archive + ".part";
        TryDelete(part);

        // HEAD 只是为了进度总量与日志，失败不致命（有的 CDN 不放 HEAD）
        long? total = null;
        try
        {
            total = await GetContentLengthAsync(PluginUri, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // HEAD 失败不阻止下载
        }

        using (var response = await SendAsync(HttpMethod.Get, PluginUri, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            total ??= response.Content.Headers.ContentLength;

            await using (var target = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            {
                var buffer = new byte[81920];
                long written = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    written += read;
                    if (total is > 0 and var size)
                    {
                        progress?.Report(Math.Clamp((double)written / size, 0d, 1d));
                    }
                }

                await target.FlushAsync(ct).ConfigureAwait(false);
                target.Flush(flushToDisk: true);
            }
        }

        // 只有在下载完整之后才出现最终文件名
        File.Move(part, archive, overwrite: true);
        progress?.Report(1d);
        return archive;
    }


    /// <summary>带 UA + Referer 的 HEAD；返回 <c>Content-Length</c>（未知则 <c>null</c>）。</summary>
    public async Task<long?> GetContentLengthAsync(string uri, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Head, uri, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response.Content.Headers.ContentLength;
    }


    /// <summary>
    /// 发一个带 <see cref="UserAgent"/> + <see cref="Referer"/> 的请求。
    /// </summary>
    /// <remarks>
    /// 即使 <see cref="_http"/> 是被注入的（可能没配默认头），这里也把两个头显式塞进
    /// <see cref="HttpRequestMessage"/>，保证 GET 与 HEAD 都带——这是 CDN 的硬要求。
    /// </remarks>
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Referer", Referer);
        request.Headers.Accept.ParseAdd("*/*");
        return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
    }


    // ─────────────────────────── 安装 ───────────────────────────

    /// <summary>
    /// 把 <c>PCGameSDK.dll</c> 解压到 <c>&lt;gameDirectory&gt;/&lt;dataDirectory&gt;/Plugins/</c>。
    /// </summary>
    /// <returns>安装后的 DLL 路径。</returns>
    /// <exception cref="InvalidDataException">
    /// 包里找不到 64 位插件条目（同时删掉缓存，下次会重新下载）。
    /// </exception>
    public async Task<string> InstallAsync(
        string gameDirectory,
        string dataDirectory,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        var archive = await EnsureArchiveAsync(progress, ct).ConfigureAwait(false);
        return InstallFromArchive(archive, gameDirectory, dataDirectory, ct);
    }


    /// <summary>按 <see cref="GameDefinition"/> 安装（数据目录从定义里取）。</summary>
    public Task<string> InstallAsync(
        string gameDirectory,
        GameDefinition definition,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
        => InstallAsync(gameDirectory, definition.DataDirectory, progress, ct);


    /// <summary>从已下载的包安装（同步主体，便于测试）。</summary>
    public static string InstallFromArchive(
        string archivePath,
        string gameDirectory,
        string dataDirectory,
        CancellationToken ct = default)
    {
        var pluginDirectory = Path.Join(gameDirectory, dataDirectory, "Plugins");
        Directory.CreateDirectory(pluginDirectory);

        var destination = Path.Join(pluginDirectory, PluginFileName);
        var temp = destination + ".part";

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entry = FindPluginEntry(archive)
                ?? throw new InvalidDataException($"B 站 SDK 包里找不到 64 位 {PluginFileName}：{archivePath}");

            ct.ThrowIfCancellationRequested();

            // 先写 .part：即使解压中途失败，游戏目录里也不会留下半截 DLL
            using (var input = entry.Open())
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
                output.Flush(flushToDisk: true);
            }

            if (new FileInfo(temp).Length == 0)
            {
                throw new InvalidDataException($"B 站 SDK 包里的 {PluginFileName} 是空文件：{entry.FullName}");
            }

            File.Move(temp, destination, overwrite: true);
            return destination;
        }
        catch (InvalidDataException)
        {
            // 包坏了：删掉缓存，下次重新下载
            TryDelete(temp);
            TryDelete(archivePath);
            throw;
        }
        catch (Exception)
        {
            TryDelete(temp);
            throw;
        }
    }


    /// <summary>删除缓存包（用户点"重新下载"时用）。</summary>
    public void ClearCache()
    {
        TryDelete(ArchivePath);
        TryDelete(ArchivePath + ".part");
    }


    /// <summary>判断插件是否已安装且非空（与 <see cref="ChannelValidator"/> 的条件 2 一致）。</summary>
    public static bool IsInstalled(string gameDirectory, GameDefinition definition)
    {
        var path = definition.GetBilibiliPluginPath(gameDirectory);
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }


    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // 删不掉不致命
        }
    }


    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}
