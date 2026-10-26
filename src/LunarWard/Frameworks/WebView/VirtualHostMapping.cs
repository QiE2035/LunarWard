namespace LunarWard.Frameworks.WebView;

/// <summary>WebView2 的 <c>CoreWebView2HostResourceAccessKind</c> 对应物。</summary>
public enum HostResourceAccessKind
{
    /// <summary>禁止跨域访问（默认）。</summary>
    Deny = 0,

    /// <summary>允许跨域访问。</summary>
    Allow = 1,

    /// <summary>允许同源、禁止跨域（WebView2 的 DenyCors）。</summary>
    DenyCors = 2,
}

/// <summary>
/// 虚拟主机名 → 本地目录的映射（<c>SetVirtualHostNameToFolderMapping</c> 等价物）。
///
/// WebView2 能让 <c>https://&lt;host&gt;/</c> 直接指向本地目录；WebKitGTK 不允许覆盖
/// https 协议，所以这里用一个自定义 scheme <see cref="Scheme"/> 承载：
/// <c>appassets://&lt;host&gt;/&lt;path&gt;</c>。主框架导航时把
/// <c>https://&lt;host&gt;/…</c> 自动改写成 <c>appassets://…</c>，页面内的相对路径和
/// <c>fetch("/x.json")</c> 都走同一个 scheme（已注册为 CORS 友好 + secure）。
///
/// 已知差异：页面里写死的绝对 URL（<c>https://&lt;host&gt;/a.js</c> 这种字符串）不会被
/// 自动改写，需要改成相对路径或 <c>appassets://</c> 形式。上游 Starward 没有任何地方
/// 使用虚拟主机映射，这个差异目前不影响移植。
/// </summary>
public static class VirtualHostMapping
{
    /// <summary>自定义 scheme 名。注册在 WebKitWebContext 上，进程内唯一。</summary>
    public const string Scheme = "appassets";

    private static readonly Dictionary<string, VirtualHost> Hosts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public sealed record VirtualHost(string Host, string Folder, HostResourceAccessKind Access);

    public static IReadOnlyCollection<VirtualHost> Mappings
    {
        get
        {
            lock (Gate)
            {
                return Hosts.Values.ToArray();
            }
        }
    }

    /// <summary>注册/更新一个映射。目录不存在时返回 false（与 WebView2 一致：静默失败）。</summary>
    public static bool Set(string host, string folder, HostResourceAccessKind access)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        try
        {
            string full = Path.GetFullPath(folder);
            if (!Directory.Exists(full))
            {
                WebViewLog.Warn($"虚拟主机映射目录不存在：{full}");
                return false;
            }

            lock (Gate)
            {
                Hosts[host] = new VirtualHost(host, full, access);
            }

            WebViewLog.Info($"虚拟主机映射：https://{host}/ -> {full}（{access}）");
            return true;
        }
        catch (Exception ex)
        {
            WebViewLog.Error($"注册虚拟主机映射失败：{host}", ex);
            return false;
        }
    }

    public static bool Remove(string host)
    {
        lock (Gate)
        {
            return Hosts.Remove(host);
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Hosts.Clear();
        }
    }

    /// <summary>把 <c>https://&lt;host&gt;/x</c> 改写成自定义 scheme；未命中返回 null。</summary>
    public static string? RewriteHttpsUri(string uri)
    {
        if (!uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string rest = uri["https://".Length..];
        int slash = rest.IndexOf('/');
        string host = slash < 0 ? rest : rest[..slash];
        string path = slash < 0 ? "/" : rest[slash..];

        lock (Gate)
        {
            if (!Hosts.ContainsKey(host))
            {
                return null;
            }
        }

        return $"{Scheme}://{host}{path}";
    }

    /// <summary>该主机是否注册过。</summary>
    public static bool IsMappedHost(string host)
    {
        lock (Gate)
        {
            return Hosts.ContainsKey(host);
        }
    }

    /// <summary>
    /// 解析 <c>appassets://host/path?query</c> 为（host, path）。非本 scheme 返回 false。
    /// 不用 System.Uri：自定义 scheme 各版本行为不一致，手写解析更可预测。
    /// </summary>
    public static bool TryParseUri(string uri, out string host, out string path)
    {
        host = string.Empty;
        path = "/";

        string prefix = Scheme + "://";
        if (!uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rest = uri[prefix.Length..];
        int queryAt = rest.IndexOf('?');
        if (queryAt >= 0)
        {
            rest = rest[..queryAt];
        }

        int hashAt = rest.IndexOf('#');
        if (hashAt >= 0)
        {
            rest = rest[..hashAt];
        }

        int slash = rest.IndexOf('/');
        host = slash < 0 ? rest : rest[..slash];
        path = slash < 0 ? "/" : rest[slash..];

        return !string.IsNullOrEmpty(host);
    }

    /// <summary>把请求路径解析成磁盘绝对路径。越界（..）或不存在时返回 false。</summary>
    public static bool TryResolveFile(string host, string requestPath, out string fullPath, out string contentType)
    {
        fullPath = string.Empty;
        contentType = "application/octet-stream";

        VirtualHost? mapping;
        lock (Gate)
        {
            Hosts.TryGetValue(host, out mapping);
        }

        if (mapping is null)
        {
            return false;
        }

        try
        {
            string relative = Uri.UnescapeDataString(requestPath);
            relative = relative.Replace('\\', '/').TrimStart('/');
            if (relative.Length == 0 || relative.EndsWith('/'))
            {
                relative += "index.html";
            }

            string root = mapping.Folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string combined = Path.GetFullPath(Path.Combine(root, relative));
            if (!combined.StartsWith(root, StringComparison.Ordinal))
            {
                WebViewLog.Warn($"拒绝越界访问：{host}{requestPath}");
                return false;
            }

            if (!File.Exists(combined))
            {
                return false;
            }

            fullPath = combined;
            contentType = ContentTypeFor(combined);
            return true;
        }
        catch (Exception ex)
        {
            WebViewLog.Error($"解析虚拟主机文件失败：{host}{requestPath}", ex);
            return false;
        }
    }

    public static string ContentTypeFor(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".js" or ".mjs" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".txt" => "text/plain; charset=utf-8",
            ".xml" => "application/xml; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".otf" => "font/otf",
            ".wasm" => "application/wasm",
            ".map" => "application/json",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mp3" => "audio/mpeg",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream",
        };
    }
}
