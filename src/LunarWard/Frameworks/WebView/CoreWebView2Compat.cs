using System.Text.Json;

namespace LunarWard.Frameworks.WebView;

// ---------------------------------------------------------------------------
// WebView2 形状的兼容 API。
//
// 目的：把上游 Starward 里 `using Microsoft.Web.WebView2.Core;` 的窗口迁过来时，
// 只需要改命名空间 + 把 `WebView2` 控件换成 `WebView`，其余代码尽量不动。
// 语义对齐点（与本文件同名类型在 WebView2 里的行为）：
//   ExecuteScriptAsync 返回 JSON 文本；字符串结果带引号。
//   WebMessageReceived 的 WebMessageAsJson 对字符串载荷是 JSON 字符串。
//   导航/标题/Cookie/截图/新窗口/进程失败 事件的名字与参数顺序保持一致。
// ---------------------------------------------------------------------------

/// <summary>虚拟主机映射的访问级别（WebView2 的 CoreWebView2HostResourceAccessKind）。</summary>
public enum CoreWebView2HostResourceAccessKind
{
    Deny = 0,
    Allow = 1,
    DenyCors = 2,
}

/// <summary>偏好配色（WebView2 的 CoreWebView2PreferredColorScheme）。</summary>
public enum CoreWebView2PreferredColorScheme
{
    Auto = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>进程失败类别。</summary>
public enum CoreWebView2ProcessFailedKind
{
    BrowserProcessExited = 0,
    RenderProcessExited = 1,
    RenderProcessUnresponsive = 2,
    FrameRenderProcessExited = 3,
    UtilityProcessExited = 4,
    SandboxHelperProcessExited = 5,
    GpuProcessExited = 6,
    Unknown = 7,
}

/// <summary>导航错误（WebKit 只给出成功/失败，这里保留少量常用值）。</summary>
public enum CoreWebView2WebErrorStatus
{
    Unknown = 0,
    OperationCanceled = 1,
    ConnectionAborted = 2,
    HostNameNotResolved = 3,
    Timeout = 4,
    ServerUnreachable = 5,
    None = 200,
}

/// <summary>控件设置（部分项在 WebKitGTK 上没有等价开关，只记录并告警）。</summary>
public sealed class CoreWebView2Settings
{
    private readonly WebView _view;
    private string? _userAgent;
    private bool _isScriptEnabled = true;

    internal CoreWebView2Settings(WebView view) => _view = view;

    /// <summary>等价 Settings.UserAgent；赋值后立刻下发给 WebKit 的 WebSettings。</summary>
    public string UserAgent
    {
        get => _userAgent ?? WebViewDefaults.DefaultUserAgent;
        set
        {
            _userAgent = value;
            _view.ApplyUserAgent(value);
        }
    }

    /// <summary>WebKitGTK 无法整体关闭脚本；置 false 只记录告警。</summary>
    public bool IsScriptEnabled
    {
        get => _isScriptEnabled;
        set
        {
            _isScriptEnabled = value;
            if (!value)
            {
                WebViewLog.Warn("WebKitGTK 不支持关闭页面脚本（IsScriptEnabled=false 被忽略）");
            }
        }
    }

    /// <summary>右键菜单：WebKitGTK 由 WebKitSettings 控制，默认开启。</summary>
    public bool AreDefaultContextMenusEnabled { get; set; } = true;

    public bool IsZoomControlEnabled { get; set; } = true;

    public bool AreDevToolsEnabled { get; set; }

    public bool IsStatusBarEnabled { get; set; }
}

/// <summary>Profile：目前只承载配色偏好。</summary>
public sealed class CoreWebView2Profile
{
    private readonly WebView _view;
    private CoreWebView2PreferredColorScheme _scheme = CoreWebView2PreferredColorScheme.Auto;

    internal CoreWebView2Profile(WebView view) => _view = view;

    public string ProfileName => "Default";

    public CoreWebView2PreferredColorScheme PreferredColorScheme
    {
        get => _scheme;
        set
        {
            _scheme = value;
            bool dark = value == CoreWebView2PreferredColorScheme.Dark
                        || (value == CoreWebView2PreferredColorScheme.Auto && _view.IsSystemDarkTheme);
            _view.ApplyDarkMode(dark);
        }
    }
}

/// <summary>Cookie（对应 WebView2 的 CoreWebView2Cookie）。</summary>
public sealed class CoreWebView2Cookie
{
    public string Name { get; internal set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Path { get; set; } = "/";
    public bool IsSecure { get; set; }
    public bool IsHttpOnly { get; set; }

    internal CookieSnapshot ToSnapshot() => new(Name, Value, Domain, Path, IsSecure, IsHttpOnly);

    internal static CoreWebView2Cookie FromSnapshot(CookieSnapshot snapshot) => new()
    {
        Name = snapshot.Name,
        Value = snapshot.Value,
        Domain = snapshot.Domain,
        Path = snapshot.Path,
        IsSecure = snapshot.Secure,
        IsHttpOnly = snapshot.HttpOnly,
    };
}

/// <summary>Cookie 管理（对应 CoreWebView2CookieManager）。</summary>
public sealed class CoreWebView2CookieManager
{
    private readonly WebView _view;

    internal CoreWebView2CookieManager(WebView view) => _view = view;

    public CoreWebView2Cookie CreateCookie(string name, string value, string domain, string path) => new()
    {
        Name = name,
        Value = value,
        Domain = domain,
        Path = path,
    };

    public async Task<IReadOnlyList<CoreWebView2Cookie>> GetCookiesAsync(string uri)
    {
        WebKitWebViewCore? core = _view.NativeCore;
        if (core is null)
        {
            return [];
        }

        IReadOnlyList<CookieSnapshot> cookies = await core.GetCookiesAsync(uri).ConfigureAwait(false);
        return cookies.Select(CoreWebView2Cookie.FromSnapshot).ToArray();
    }

    /// <summary>同步签名与 WebView2 一致；底层是异步的，失败只记录日志。</summary>
    public void AddOrUpdateCookie(CoreWebView2Cookie cookie)
    {
        _ = AddOrUpdateCookieAsync(cookie);
    }

    public async Task AddOrUpdateCookieAsync(CoreWebView2Cookie cookie)
    {
        WebKitWebViewCore? core = _view.NativeCore;
        if (core is null)
        {
            return;
        }

        bool ok = await core.SetCookieAsync(cookie.ToSnapshot()).ConfigureAwait(false);
        if (!ok)
        {
            WebViewLog.Warn($"AddOrUpdateCookie 失败：{cookie.Name}@{cookie.Domain}");
        }
    }

    public void DeleteCookie(CoreWebView2Cookie cookie)
    {
        _ = DeleteCookieAsync(cookie);
    }

    public async Task DeleteCookieAsync(CoreWebView2Cookie cookie)
    {
        WebKitWebViewCore? core = _view.NativeCore;
        if (core is null)
        {
            return;
        }

        await core.DeleteCookieAsync(cookie.ToSnapshot()).ConfigureAwait(false);
    }

    public void DeleteAllCookies() => _view.NativeCore?.ClearCookies();
}

// ------------------------------------------------------------------ 事件参数

public sealed class CoreWebView2NavigationStartingEventArgs(string uri) : EventArgs
{
    public string Uri { get; } = uri;

    public bool IsUserInitiated { get; internal set; }

    /// <summary>WebKitGTK 的 decide-policy 未接入，取消请求会被忽略。</summary>
    public bool Cancel
    {
        get => false;
        set
        {
            if (value)
            {
                WebViewLog.Warn($"忽略导航取消请求：{Uri}（decide-policy 未接入）");
            }
        }
    }
}

public sealed class CoreWebView2NavigationCompletedEventArgs(string uri, bool isSuccess, string? error) : EventArgs
{
    public string Uri { get; } = uri;

    public bool IsSuccess { get; } = isSuccess;

    public CoreWebView2WebErrorStatus WebErrorStatus { get; } = isSuccess ? CoreWebView2WebErrorStatus.None : CoreWebView2WebErrorStatus.Unknown;

    public string? ErrorText { get; } = error;

    public int HttpStatusCode { get; internal set; }
}

public sealed class CoreWebView2SourceChangedEventArgs(bool isNewDocument) : EventArgs
{
    public bool IsNewDocument { get; } = isNewDocument;
}

public sealed class CoreWebView2DocumentTitleChangedEventArgs(string title) : EventArgs
{
    public string Title { get; } = title;
}

public sealed class CoreWebView2HistoryChangedEventArgs : EventArgs;

public sealed class CoreWebView2NewWindowRequestedEventArgs(string uri) : EventArgs
{
    public string Uri { get; } = uri;

    /// <summary>上游一律置 true 并把 URI 交给系统浏览器；WebKit 侧窗口创建已被拦截。</summary>
    public bool Handled { get; set; }
}

public sealed class CoreWebView2ProcessFailedEventArgs(CoreWebView2ProcessFailedKind kind) : EventArgs
{
    public CoreWebView2ProcessFailedKind ProcessFailedKind { get; } = kind;
}

/// <summary>Web 消息（WebMessageAsJson / TryGetWebMessageAsString 语义与 WebView2 一致）。</summary>
public sealed class CoreWebView2WebMessageReceivedEventArgs(string webMessageAsJson, string source)
    : EventArgs
{
    public string Source { get; } = source;

    public string WebMessageAsJson { get; } = webMessageAsJson;

    /// <summary>仅在载荷是字符串时返回 true（与 WebView2 一致）。</summary>
    public bool TryGetWebMessageAsString(out string value)
    {
        value = string.Empty;
        if (string.IsNullOrEmpty(WebMessageAsJson) || WebMessageAsJson[0] != '"')
        {
            return false;
        }

        try
        {
            string? parsed = JsonSerializer.Deserialize<string>(WebMessageAsJson);
            if (parsed is null)
            {
                return false;
            }

            value = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>资源响应（对应 CoreWebView2WebResourceResponseView）。</summary>
public sealed class CoreWebView2WebResourceResponseView(int statusCode, string mimeType, Func<byte[]?> content)
{
    public int StatusCode { get; } = statusCode;

    public string ContentType { get; } = mimeType;

    /// <summary>等价 GetContentAsync：拉取响应体。</summary>
    public Task<Stream?> GetContentAsync()
    {
        byte[]? bytes = content();
        return Task.FromResult<Stream?>(bytes is null ? null : new MemoryStream(bytes, writable: false));
    }
}

/// <summary>资源响应事件参数。</summary>
public sealed class CoreWebView2WebResourceResponseReceivedEventArgs(string uri, CoreWebView2WebResourceResponseView response)
    : EventArgs
{
    public string Uri { get; } = uri;

    public CoreWebView2WebResourceResponseView Response { get; } = response;
}

// ---------------------------------------------------------------- 主门面

/// <summary>对应 CoreWebView2。</summary>
public sealed class CoreWebView2
{
    private const string Scheme = VirtualHostMapping.Scheme;

    private readonly WebView _view;
    private readonly Action<Action>? _dispatch;
    private double _zoomFactor = 1.0;
    private System.Drawing.Color _defaultBackgroundColor = System.Drawing.Color.Transparent;

    internal CoreWebView2(WebView view, WebKitWebViewCore core, Action<Action>? dispatch)
    {
        _view = view;
        _dispatch = dispatch;
        Settings = new CoreWebView2Settings(view);
        Profile = new CoreWebView2Profile(view);
        CookieManager = new CoreWebView2CookieManager(view);
        WireEvents(core);
    }

    public CoreWebView2Settings Settings { get; }

    public CoreWebView2Profile Profile { get; }

    public CoreWebView2CookieManager CookieManager { get; }

    public string BrowserVersion => WebViewAvailability.GetBrowserVersionString();

    /// <summary>页面缩放倍数（WebView2 的 CoreWebView2Controller.ZoomFactor）。默认 1.0。</summary>
    public double ZoomFactor
    {
        get => _zoomFactor;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "ZoomFactor 必须大于 0");
            }

            _zoomFactor = value;
            WebKitWebViewCore? core = _view.NativeCore;
            if (core is null)
            {
                WebViewLog.Warn($"ZoomFactor={value} 在 WebView 初始化前设置，已忽略");
                return;
            }

            core.SetZoomFactor(value);
        }
    }

    /// <summary>
    /// 默认背景色（WebView2 的 CoreWebView2Controller.DefaultBackgroundColor）。
    /// 页面未绘制区域露出的颜色；Transparent 表示交给宿主的背景。
    /// </summary>
    public System.Drawing.Color DefaultBackgroundColor
    {
        get => _defaultBackgroundColor;
        set
        {
            _defaultBackgroundColor = value;
            WebKitWebViewCore? core = _view.NativeCore;
            if (core is null)
            {
                WebViewLog.Warn($"DefaultBackgroundColor={value} 在 WebView 初始化前设置，已忽略");
                return;
            }

            core.SetBackgroundColor(value.R, value.G, value.B, value.A);
        }
    }

    /// <summary>
    /// 虚拟主机名 → 本地目录映射（WebView2 的 SetVirtualHostNameToFolderMapping）。
    /// WebKitGTK 不能劫持 https，等价实现是自定义 scheme <c>appassets://&lt;host&gt;/&lt;path&gt;</c>，
    /// 主框架导航到 <c>https://&lt;host&gt;/…</c> 时会被改写成该 scheme；页面里硬编码的绝对
    /// 子资源 URL 不会被改写（见 docs/09-LINUX-DIVERGENCES.md 五）。
    /// </summary>
    public void SetVirtualHostNameToFolderMapping(
        string hostName,
        string folderPath,
        CoreWebView2HostResourceAccessKind accessKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        var access = accessKind switch
        {
            CoreWebView2HostResourceAccessKind.Allow => HostResourceAccessKind.Allow,
            CoreWebView2HostResourceAccessKind.DenyCors => HostResourceAccessKind.DenyCors,
            _ => HostResourceAccessKind.Deny,
        };

        if (!VirtualHostMapping.Set(hostName, folderPath, access))
        {
            WebViewLog.Warn($"SetVirtualHostNameToFolderMapping：目录 '{folderPath}' 不存在，已忽略（host={hostName}）");
            return;
        }

        WebViewLog.Info($"虚拟主机映射：https://{hostName}/ → {Scheme}://{hostName}/（{access}，目录 {folderPath}）");
    }

    /// <summary>移除一个虚拟主机映射（WebView2 的 ClearVirtualHostNameToFolderMapping）。</summary>
    public bool ClearVirtualHostNameToFolderMapping(string hostName) => VirtualHostMapping.Remove(hostName);

    public Uri? Source
    {
        get => _view.Source;
        set => _view.Source = value;
    }

    /// <summary>等价 CoreWebView2.DocumentTitle（WebKitGTK 的 webkit_web_view_get_title）。</summary>
    public string DocumentTitle => _view.NativeCore?.DocumentTitle ?? string.Empty;

    public bool CanGoBack => _view.NativeCore?.CanGoBack() ?? false;

    public bool CanGoForward => _view.NativeCore?.CanGoForward() ?? false;

    public event EventHandler<CoreWebView2NavigationStartingEventArgs>? NavigationStarting;

    public event EventHandler<CoreWebView2NavigationCompletedEventArgs>? NavigationCompleted;

    public event EventHandler<object?>? DOMContentLoaded;

    public event EventHandler<CoreWebView2WebMessageReceivedEventArgs>? WebMessageReceived;

    public event EventHandler<CoreWebView2SourceChangedEventArgs>? SourceChanged;

    public event EventHandler<CoreWebView2DocumentTitleChangedEventArgs>? DocumentTitleChanged;

    public event EventHandler<CoreWebView2HistoryChangedEventArgs>? HistoryChanged;

    public event EventHandler<CoreWebView2NewWindowRequestedEventArgs>? NewWindowRequested;

    public event EventHandler<CoreWebView2ProcessFailedEventArgs>? ProcessFailed;

    public event EventHandler<CoreWebView2WebResourceResponseReceivedEventArgs>? WebResourceResponseReceived;

    public Task<string> ExecuteScriptAsync(string javaScript)
    {
        WebKitWebViewCore? core = _view.NativeCore;
        if (core is null)
        {
            throw new InvalidOperationException("WebView 尚未初始化，请先 await EnsureCoreWebView2Async()");
        }

        return core.ExecuteScriptAsync(javaScript);
    }

    public void Navigate(string uri) => _view.Navigate(uri);

    public void NavigateToString(string html) => _view.NativeCore?.NavigateToString(html);

    public void Reload() => _view.NativeCore?.Reload();

    public void Stop() => _view.NativeCore?.Stop();

    public void GoBack() => _view.NativeCore?.GoBack();

    public void GoForward() => _view.NativeCore?.GoForward();

    public void OpenDevToolsWindow() =>
        WebViewLog.Warn("OpenDevToolsWindow 未实现：WebKitGTK 需要 WebKitSettings.enable-developer-extras + inspector 端口，见 docs/09-LINUX-DIVERGENCES.md");

    /// <summary>
    /// DevTools 协议子集：只支持 <c>Page.captureScreenshot</c>（等价实现走 WebKit 的
    /// webkit_web_view_get_snapshot，不需要调试端口）。其他方法抛 PlatformNotSupportedException。
    /// </summary>
    public async Task<string> CallDevToolsProtocolMethodAsync(string methodName, string parametersJson)
    {
        WebKitWebViewCore? core = _view.NativeCore
                                  ?? throw new InvalidOperationException("WebView 尚未初始化");

        if (string.Equals(methodName, "Page.captureScreenshot", StringComparison.OrdinalIgnoreCase))
        {
            byte[] png = await core.CaptureSnapshotAsync(fullDocument: false).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { data = Convert.ToBase64String(png) });
        }

        throw new PlatformNotSupportedException(
            $"CallDevToolsProtocolMethodAsync('{methodName}') 在 WebKitGTK 后端没有等价实现；"
            + "截图请用 Page.captureScreenshot，其它 CDP 能力请改用 WebKit 原生 API。");
    }

    /// <summary>等价 CapturePreviewAsync：返回 PNG 字节流。</summary>
    public async Task<Stream> CapturePreviewAsync()
    {
        WebKitWebViewCore? core = _view.NativeCore ?? throw new InvalidOperationException("WebView 尚未初始化");
        byte[] png = await core.CaptureSnapshotAsync(fullDocument: false).ConfigureAwait(false);
        return new MemoryStream(png, writable: false);
    }

    internal void RaiseDispatch(Action action) => _dispatch?.Invoke(action);

    private void WireEvents(WebKitWebViewCore core)
    {
        core.Dispatch = action => _view.PostToUi(action);
        core.EnableResourceSniffing = true;

        core.NavigationStarting += uri =>
            NavigationStarting?.Invoke(this, new CoreWebView2NavigationStartingEventArgs(uri));

        core.NavigationCompleted += (uri, success) =>
        {
            NavigationCompleted?.Invoke(this, new CoreWebView2NavigationCompletedEventArgs(uri, success, core.LastErrorText));
            HistoryChanged?.Invoke(this, new CoreWebView2HistoryChangedEventArgs());
        };

        core.DocumentLoaded += () => DOMContentLoaded?.Invoke(this, null);

        core.MessageReceived += json =>
            WebMessageReceived?.Invoke(this, new CoreWebView2WebMessageReceivedEventArgs(json, core.CurrentUri));

        core.SourceChanged += uri =>
            SourceChanged?.Invoke(this, new CoreWebView2SourceChangedEventArgs(isNewDocument: true));

        core.TitleChanged += title =>
            DocumentTitleChanged?.Invoke(this, new CoreWebView2DocumentTitleChangedEventArgs(title));

        core.NewWindowRequested += uri => NewWindowRequested?.Invoke(this, new CoreWebView2NewWindowRequestedEventArgs(uri));

        core.ProcessFailed += reason =>
        {
            var kind = reason switch
            {
                WebViewProcessFailureReason.ExceededMemoryLimit => CoreWebView2ProcessFailedKind.UtilityProcessExited,
                WebViewProcessFailureReason.Terminated => CoreWebView2ProcessFailedKind.BrowserProcessExited,
                _ => CoreWebView2ProcessFailedKind.RenderProcessExited,
            };
            ProcessFailed?.Invoke(this, new CoreWebView2ProcessFailedEventArgs(kind));
        };

        core.ResourceResponseReceived += (uri, status, mime, content) =>
        {
            var response = new CoreWebView2WebResourceResponseView(status, mime, content);
            WebResourceResponseReceived?.Invoke(this, new CoreWebView2WebResourceResponseReceivedEventArgs(uri, response));
        };

        HistoryChanged?.Invoke(this, new CoreWebView2HistoryChangedEventArgs());
    }
}

/// <summary>
/// 对应 CoreWebView2Environment：WebKitGTK 没有可单独安装的运行时，这里返回库版本。
/// </summary>
public static class CoreWebView2Environment
{
    public static string GetAvailableBrowserVersionString() => WebViewAvailability.GetBrowserVersionString();

    public static bool IsSupported => WebViewAvailability.IsAvailable;
}

/// <summary>默认值集中在这里，方便与上游对齐。</summary>
public static class WebViewDefaults
{
    /// <summary>上游 UpdateWindow 会在末尾追 " Starward/{version}"。</summary>
    public const string UserAgentSuffixKey = "LunarWard";

    public static string DefaultUserAgent
    {
        get
        {
            // WebKit 默认 UA 已在原生层生效；这里只用于读取展示。
            return $"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/605.1.15 (KHTML, like Gecko) {UserAgentSuffixKey}/1.0 Safari/605.1.15";
        }
    }
}
