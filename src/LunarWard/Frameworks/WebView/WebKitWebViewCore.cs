using System.Runtime.InteropServices;
using System.Text.Json;
using LunarWard.Frameworks.WebView.Interop;

namespace LunarWard.Frameworks.WebView;

/// <summary>创建原生视图的参数。</summary>
internal sealed record WebViewCreateOptions(
    ulong ParentXid,
    string WindowTitle,
    int Width,
    int Height,
    string? UserAgent,
    bool DarkMode,
    bool DeveloperExtras);

/// <summary>Cookie 快照（与 SoupCookie 解耦，可跨线程传递）。</summary>
public sealed record CookieSnapshot(
    string Name,
    string Value,
    string Domain,
    string Path,
    bool Secure,
    bool HttpOnly);

/// <summary>Web 进程终止原因。</summary>
public enum WebViewProcessFailureReason
{
    Crashed = 0,
    ExceededMemoryLimit = 1,
    Terminated = 2,
}

/// <summary>脚本执行包装方式。</summary>
public enum ScriptWrapMode
{
    /// <summary>放进函数里 eval：返回完成值，且顶层 let/var 不泄漏到全局（贴近 WebView2）。</summary>
    Isolated = 0,

    /// <summary>原样注入主世界（无法回避 CSP unsafe-eval 时的退路）。</summary>
    Raw = 1,
}

/// <summary>脚本包装器（纯逻辑）。</summary>
public static class ScriptWrapper
{
    /// <summary>把脚本包成一次表达式求值，返回值即脚本的完成值。</summary>
    public static string Wrap(string script, ScriptWrapMode mode)
    {
        script ??= string.Empty;
        if (mode == ScriptWrapMode.Raw)
        {
            return script;
        }

        // JSON 字面量承载脚本文本，规避引号/换行/反斜杠转义问题。
        string literal = JsonSerializer.Serialize(script);
        return $"__lunarwardRun({literal});";
    }

    /// <summary>document-start 注入的求值器；非严格 eval，this 指向 window。</summary>
    public const string RunnerScript = """
        (function () {
          if (window.__lunarwardRun) { return; }
          Object.defineProperty(window, '__lunarwardRun', {
            configurable: true,
            writable: true,
            value: function (source) { return eval(source); }
          });
        })();
        """;
}

/// <summary>脚本抛出异常时带回 .NET 的异常类型。</summary>
public sealed class JavaScriptException : Exception
{
    public JavaScriptException(string message) : base(message)
    {
    }
}

/// <summary>
/// WebKitWebView 的原生包装。公开方法可在任意线程调用，内部切到 GTK 线程执行；
/// 事件默认在 GTK 线程触发，控件层通过 <see cref="Dispatch"/> 切回 UI 线程。
/// </summary>
internal sealed class WebKitWebViewCore : IDisposable
{
    private const string MessageHandlerName = "lunarward";
    private const string InternalHandlerName = "lunarwardinternal";

    /// <summary>window.chrome.webview 兼容层（document-start 注入）。</summary>
    private const string BridgeScript = """
        (function () {
          if (window.chrome && window.chrome.webview && window.chrome.webview.__lunarward) { return; }
          var handlers = window.webkit && window.webkit.messageHandlers;
          var native = handlers && handlers.lunarward;
          if (!native) { return; }
          var listeners = [];
          function raise(message) {
            var event = { data: message, source: bridge, origin: (window.location && window.location.origin) || '' };
            for (var i = 0; i < listeners.length; i++) {
              try { listeners[i](event); } catch (e) { setTimeout(function () { throw e; }, 0); }
            }
          }
          var bridge = {
            __lunarward: true,
            postMessage: function (message) { native.postMessage(message); },
            addEventListener: function (type, listener) {
              if (type === 'message' && typeof listener === 'function' && listeners.indexOf(listener) < 0) { listeners.push(listener); }
            },
            removeEventListener: function (type, listener) {
              var i = listeners.indexOf(listener);
              if (type === 'message' && i >= 0) { listeners.splice(i, 1); }
            },
            postMessageFromHost: raise
          };
          window.chrome = window.chrome || {};
          window.chrome.webview = bridge;
        })();
        """;

    /// <summary>DOMContentLoaded 等价信号：WebKitGTK 4.1 无对应信号，用注入脚本回报。</summary>
    private const string DomContentLoadedScript = """
        document.addEventListener('DOMContentLoaded', function () {
          try {
            var handlers = window.webkit && window.webkit.messageHandlers;
            if (handlers && handlers.lunarwardinternal) {
              handlers.lunarwardinternal.postMessage({ event: 'DOMContentLoaded' });
            }
          } catch (e) { }
        });
        """;

    // ------------------------------------------------------------- 委托类型

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LoadChangedDelegate(IntPtr view, int loadEvent, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int LoadFailedDelegate(IntPtr view, int loadEvent, IntPtr failingUri, IntPtr error, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PropertyNotifyDelegate(IntPtr obj, IntPtr pspec, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ProcessTerminatedDelegate(IntPtr view, int reason, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateWindowDelegate(IntPtr view, IntPtr navigationAction, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ResourceStartedDelegate(IntPtr view, IntPtr resource, IntPtr request, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ScriptMessageDelegate(IntPtr userContentManager, IntPtr jsResult, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ResourceFinishedDelegate(IntPtr resource, IntPtr data);

    // 静态委托实例：保证生命周期覆盖所有原生回调。
    private static readonly GAsyncReadyCallback EvalCallbackDelegate = OnEvalFinished;
    private static readonly GAsyncReadyCallback RawEvalCallbackDelegate = OnRawEvalFinished;
    private static readonly GAsyncReadyCallback SnapshotCallbackDelegate = OnSnapshotFinished;
    private static readonly GAsyncReadyCallback CookiesCallbackDelegate = OnGetCookiesFinished;
    private static readonly GAsyncReadyCallback AllCookiesCallbackDelegate = OnGetAllCookiesFinished;
    private static readonly GAsyncReadyCallback CookieWriteCallbackDelegate = OnCookieWriteFinished;
    private static readonly GAsyncReadyCallback ResourceDataCallbackDelegate = OnResourceDataFinished;
    private static readonly WebKitURISchemeRequestCallback UriSchemeDelegate = OnUriSchemeRequest;
    private static readonly ResourceFinishedDelegate ResourceFinishedHandler = OnResourceFinished;
    private static readonly GDestroyNotify SoupCookieDestroy = static cookie => Soup.soup_cookie_free(cookie);
    private static readonly IntPtr SoupCookieDestroyPointer = Marshal.GetFunctionPointerForDelegate(SoupCookieDestroy);

    private static readonly object SchemeGate = new();
    private static readonly HashSet<IntPtr> SchemeRegisteredContexts = [];

    // 每实例委托（必须持有引用，否则会被 GC 回收导致原生回调崩溃）。
    private readonly LoadChangedDelegate _loadChangedDelegate;
    private readonly LoadFailedDelegate _loadFailedDelegate;
    private readonly PropertyNotifyDelegate _uriNotifyDelegate;
    private readonly PropertyNotifyDelegate _titleNotifyDelegate;
    private readonly ProcessTerminatedDelegate _processTerminatedDelegate;
    private readonly CreateWindowDelegate _createWindowDelegate;
    private readonly ResourceStartedDelegate _resourceStartedDelegate;
    private readonly ScriptMessageDelegate _messageDelegate;
    private readonly ScriptMessageDelegate _internalMessageDelegate;

    private readonly IntPtr _view;
    private readonly IntPtr _container;
    private readonly IntPtr _userContentManager;
    private readonly GCHandle _selfHandle;
    private readonly List<(IntPtr Instance, ulong Id)> _connections = [];

    private bool _disposed;
    private string? _loadError;
    private string _currentUri = string.Empty;
    private string _documentTitle = string.Empty;
    private bool _enableResourceSniffing;

    private WebKitWebViewCore(IntPtr view, IntPtr container, IntPtr userContentManager, bool embedded)
    {
        _view = view;
        _container = container;
        _userContentManager = userContentManager;
        IsEmbedded = embedded;
        _selfHandle = GCHandle.Alloc(this);

        _loadChangedDelegate = OnLoadChanged;
        _loadFailedDelegate = OnLoadFailed;
        _uriNotifyDelegate = OnUriNotify;
        _titleNotifyDelegate = OnTitleNotify;
        _processTerminatedDelegate = OnProcessTerminated;
        _createWindowDelegate = OnCreateNewWindow;
        _resourceStartedDelegate = OnResourceLoadStarted;
        _messageDelegate = OnScriptMessage;
        _internalMessageDelegate = OnInternalScriptMessage;
    }

    public IntPtr View => _view;

    public IntPtr Container => _container;

    /// <summary>true = 以 GtkPlug 嵌入外部 X11 窗口；false = 独立 GTK 窗口（降级）。</summary>
    public bool IsEmbedded { get; }

    public string CurrentUri => _currentUri;

    public string DocumentTitle => _documentTitle;

    /// <summary>最近一次导航失败的原因（成功导航后清空）。</summary>
    public string? LastErrorText => _loadError;

    /// <summary>把 GTK 线程上的事件回抛到 UI 线程；为 null 则原地触发。</summary>
    public Action<Action>? Dispatch { get; set; }

    public bool EnableResourceSniffing
    {
        get => _enableResourceSniffing;
        set => _enableResourceSniffing = value;
    }

    // ------------------------------------------------------------------ 事件

    public event Action<string>? NavigationStarting;

    public event Action<string, bool>? NavigationCompleted;

    public event Action? DocumentLoaded;

    public event Action<string>? MessageReceived;

    public event Action<string>? SourceChanged;

    public event Action<string>? TitleChanged;

    public event Action<string>? NewWindowRequested;

    public event Action<WebViewProcessFailureReason>? ProcessFailed;

    public event Action<string, int, string, Func<byte[]?>>? ResourceResponseReceived;

    private void Raise(Action action)
    {
        Action<Action>? dispatch = Dispatch;
        if (dispatch is null)
        {
            Invoke(action);
            return;
        }

        try
        {
            dispatch(action);
        }
        catch (Exception ex)
        {
            WebViewLog.Error("把 WebView 事件回抛到 UI 线程失败", ex);
            Invoke(action);
        }
    }

    private static void Invoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            WebViewLog.Error("WebView 事件处理抛异常", ex);
        }
    }

    // ------------------------------------------------------------ 创建 / 销毁

    /// <summary>必须在 GTK 线程调用。</summary>
    public static WebKitWebViewCore Create(WebViewCreateOptions options)
    {
        if (!GtkRuntime.IsGtkThread)
        {
            throw new InvalidOperationException("WebKitWebViewCore.Create 必须在 GTK 线程调用");
        }

        IntPtr userContentManager = WebKit.webkit_user_content_manager_new();
        WebKit.webkit_user_content_manager_register_script_message_handler(userContentManager, MessageHandlerName);
        WebKit.webkit_user_content_manager_register_script_message_handler(userContentManager, InternalHandlerName);

        AddUserScript(userContentManager, ScriptWrapper.RunnerScript, WebKitUserScriptInjectionTime.DocumentStart);
        AddUserScript(userContentManager, BridgeScript, WebKitUserScriptInjectionTime.DocumentStart);
        AddUserScript(userContentManager, DomContentLoadedScript, WebKitUserScriptInjectionTime.DocumentStart);

        IntPtr view = WebKit.webkit_web_view_new_with_user_content_manager(userContentManager);
        if (view == IntPtr.Zero)
        {
            throw new InvalidOperationException("webkit_web_view_new_with_user_content_manager 返回 NULL");
        }

        ApplySettings(view, options);

        bool embedded = options.ParentXid != 0;
        IntPtr container;
        if (embedded)
        {
            container = Gtk.gtk_plug_new(options.ParentXid);
            Gtk.gtk_container_add(container, view);

            Gtk.gtk_widget_set_size_request(view, Math.Max(1, options.Width), Math.Max(1, options.Height));
            Gtk.gtk_widget_show_all(container);
            Gtk.gtk_widget_realize(container);

            // GtkPlug 是 GtkWindow 的子类，尺寸由「宿主 socket」决定；我们的 socket 是
            // 一个不会主动发 XEMBED 尺寸协商的普通 X11 窗口（Avalonia 的 XID 也一样），
            // 所以必须自己给 plug 分配矩形，否则分配结果停在 1x1。
            SizeAllocate(container, 0, 0, Math.Max(1, options.Width), Math.Max(1, options.Height));

            WebViewLog.Info(
                $"GtkPlug 嵌入：socket=0x{options.ParentXid:X} plug=0x{Gtk.gtk_plug_get_id(container):X} embedded={Gtk.gtk_plug_get_embedded(container)}");
        }
        else
        {
            container = Gtk.gtk_window_new(GtkWindowType.Toplevel);
            Gtk.gtk_window_set_title(container, options.WindowTitle);
            Gtk.gtk_window_set_default_size(container, Math.Max(320, options.Width), Math.Max(240, options.Height));
            Gtk.gtk_container_add(container, view);
            Gtk.gtk_widget_show_all(container);
            Gtk.gtk_window_present(container);
            WebViewLog.Info("独立 GTK 窗口已创建（未嵌入宿主）");
        }

        var core = new WebKitWebViewCore(view, container, userContentManager, embedded);
        EnsureUriSchemeRegistered(view);
        core.ConnectSignals();
        return core;
    }

    private static void ApplySettings(IntPtr view, WebViewCreateOptions options)
    {
        IntPtr settings = WebKit.webkit_web_view_get_settings(view);
        if (settings != IntPtr.Zero)
        {
            if (!string.IsNullOrEmpty(options.UserAgent))
            {
                WebKit.webkit_settings_set_user_agent(settings, options.UserAgent);
            }

            WebKit.webkit_settings_set_enable_developer_extras(settings, options.DeveloperExtras);
            WebKit.webkit_settings_set_enable_webgl(settings, true);
            WebKit.webkit_settings_set_enable_webaudio(settings, true);
        }

        // prefers-color-scheme：WebKitGTK 跟随 GTK 主题，没有 per-view API。
        SetGtkDarkPreference(options.DarkMode);

        // WebView2 DefaultBackgroundColor 默认白色；WebKit 默认透明会闪黑。
        var white = new GdkRGBA { Red = 1, Green = 1, Blue = 1, Alpha = 1 };
        WebKit.webkit_web_view_set_background_color(view, ref white);
    }

    private static void SetGtkDarkPreference(bool dark)
    {
        IntPtr gtkSettings = Gtk.gtk_settings_get_default();
        if (gtkSettings == IntPtr.Zero)
        {
            return;
        }

        IntPtr value = Marshal.AllocHGlobal(32);
        try
        {
            Marshal.Copy(new byte[32], 0, value, 32);
            Glib.g_value_init(value, Glib.GTypeBoolean);
            Glib.g_value_set_boolean(value, dark);
            Glib.g_object_set_property(gtkSettings, "gtk-application-prefer-dark-theme", value);
            Glib.g_value_unset(value);
            WebViewLog.Info($"gtk-application-prefer-dark-theme = {dark.ToString().ToLowerInvariant()}");
        }
        catch (Exception ex)
        {
            WebViewLog.Warn($"设置 GTK 深色偏好失败：{ex.Message}");
        }
        finally
        {
            Marshal.FreeHGlobal(value);
        }
    }

    private static void AddUserScript(IntPtr userContentManager, string source, WebKitUserScriptInjectionTime injectionTime)
    {
        IntPtr script = WebKit.webkit_user_script_new(
            source,
            WebKitUserContentInjectedFrames.AllFrames,
            injectionTime,
            IntPtr.Zero,
            IntPtr.Zero);
        if (script == IntPtr.Zero)
        {
            WebViewLog.Warn("webkit_user_script_new 返回 NULL，注入脚本被跳过");
            return;
        }

        WebKit.webkit_user_content_manager_add_script(userContentManager, script);
        WebKit.webkit_user_script_unref(script);
    }

    private void ConnectSignals()
    {
        IntPtr data = GCHandle.ToIntPtr(_selfHandle);

        Attach(_view, "load-changed", _loadChangedDelegate, data);
        Attach(_view, "load-failed", _loadFailedDelegate, data);
        Attach(_view, "notify::uri", _uriNotifyDelegate, data);
        Attach(_view, "notify::title", _titleNotifyDelegate, data);
        Attach(_view, "web-process-terminated", _processTerminatedDelegate, data);
        Attach(_view, "create", _createWindowDelegate, data);
        Attach(_view, "resource-load-started", _resourceStartedDelegate, data);
        Attach(_userContentManager, "script-message-received::" + MessageHandlerName, _messageDelegate, data);
        Attach(_userContentManager, "script-message-received::" + InternalHandlerName, _internalMessageDelegate, data);
    }

    private void Attach(IntPtr instance, string signal, Delegate handler, IntPtr data)
    {
        IntPtr pointer = Marshal.GetFunctionPointerForDelegate(handler);
        ulong id = Glib.g_signal_connect_data(instance, signal, pointer, data, IntPtr.Zero, 0);
        if (id == 0)
        {
            WebViewLog.Warn($"信号连接失败：{signal}");
            return;
        }

        _connections.Add((instance, id));
    }

    // ------------------------------------------------------------ 信号回调

    private static WebKitWebViewCore? CoreOf(IntPtr data)
    {
        if (data == IntPtr.Zero)
        {
            return null;
        }

        return GCHandle.FromIntPtr(data).Target as WebKitWebViewCore;
    }

    private static string Utf8(IntPtr ptr) => ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;

    private static void OnLoadChanged(IntPtr view, int loadEvent, IntPtr data)
    {
        try
        {
            WebKitWebViewCore? core = CoreOf(data);
            if (core is null)
            {
                return;
            }

            var evt = (WebKitLoadEvent)loadEvent;
            switch (evt)
            {
                case WebKitLoadEvent.Started:
                    core._loadError = null;
                    string starting = Utf8(WebKit.webkit_web_view_get_uri(view));
                    core.Raise(() => core.NavigationStarting?.Invoke(starting));
                    break;

                case WebKitLoadEvent.Committed:
                    core.RefreshLocation();
                    break;

                case WebKitLoadEvent.Finished:
                {
                    core.RefreshLocation();
                    string uri = core._currentUri;
                    string? error = core._loadError;
                    bool success = error is null;
                    if (!success)
                    {
                        WebViewLog.Warn($"页面加载未成功：{uri} :: {error}");
                    }

                    core.Raise(() => core.NavigationCompleted?.Invoke(uri, success));
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            WebViewLog.Error("load-changed 处理异常", ex);
        }
    }

    private static int OnLoadFailed(IntPtr view, int loadEvent, IntPtr failingUri, IntPtr error, IntPtr data)
    {
        try
        {
            WebKitWebViewCore? core = CoreOf(data);
            string uri = Utf8(failingUri);
            string message = ReadGErrorMessage(error) ?? "unknown";
            if (core is not null)
            {
                core._loadError = $"{uri}: {message}";
            }

            WebViewLog.Warn($"load-failed：event={(WebKitLoadEvent)loadEvent} uri={uri} error={message}");
        }
        catch (Exception ex)
        {
            WebViewLog.Error("load-failed 处理异常", ex);
        }

        // 返回 FALSE 让 WebKit 走自己的错误页；失败状态通过 NavigationCompleted(success=false) 上报。
        return 0;
    }

    private static string? ReadGErrorMessage(IntPtr error)
    {
        if (error == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            // GError { GQuark domain; gint code; gchar *message; } —— 64 位上 message 在 +8。
            IntPtr messagePtr = Marshal.ReadIntPtr(error, 8);
            return messagePtr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(messagePtr);
        }
        catch
        {
            return null;
        }
    }

    private static void FreeGError(IntPtr error)
    {
        if (error != IntPtr.Zero)
        {
            try
            {
                Glib.g_error_free(error);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static void OnUriNotify(IntPtr obj, IntPtr pspec, IntPtr data)
    {
        try
        {
            CoreOf(data)?.RefreshLocation();
        }
        catch (Exception ex)
        {
            WebViewLog.Error("notify::uri 处理异常", ex);
        }
    }

    private static void OnTitleNotify(IntPtr obj, IntPtr pspec, IntPtr data)
    {
        try
        {
            WebKitWebViewCore? core = CoreOf(data);
            if (core is null)
            {
                return;
            }

            string title = Utf8(WebKit.webkit_web_view_get_title(obj));
            if (title == core._documentTitle)
            {
                return;
            }

            core._documentTitle = title;
            core.Raise(() => core.TitleChanged?.Invoke(title));
        }
        catch (Exception ex)
        {
            WebViewLog.Error("notify::title 处理异常", ex);
        }
    }

    private static void OnProcessTerminated(IntPtr view, int reason, IntPtr data)
    {
        try
        {
            WebKitWebViewCore? core = CoreOf(data);
            var mapped = (WebKitWebProcessTerminationReason)reason switch
            {
                WebKitWebProcessTerminationReason.ExceededMemoryLimit => WebViewProcessFailureReason.ExceededMemoryLimit,
                WebKitWebProcessTerminationReason.TerminatedByApi => WebViewProcessFailureReason.Terminated,
                _ => WebViewProcessFailureReason.Crashed,
            };
            WebViewLog.Error($"Web 进程异常终止：{mapped}");
            core?.Raise(() => core.ProcessFailed?.Invoke(mapped));
        }
        catch (Exception ex)
        {
            WebViewLog.Error("web-process-terminated 处理异常", ex);
        }
    }

    private static IntPtr OnCreateNewWindow(IntPtr view, IntPtr navigationAction, IntPtr data)
    {
        try
        {
            string uri = string.Empty;
            IntPtr request = WebKit.webkit_navigation_action_get_request(navigationAction);
            if (request != IntPtr.Zero)
            {
                uri = Utf8(WebKit.webkit_uri_request_get_uri(request));
            }

            WebViewLog.Info($"window.open / target=_blank 被拦截：{uri}");
            CoreOf(data)?.Raise(() => CoreOf(data)?.NewWindowRequested?.Invoke(uri));
        }
        catch (Exception ex)
        {
            WebViewLog.Error("create 处理异常", ex);
        }

        // 返回 NULL：不创建新窗口（上游 NewWindowRequested 一律 Handled=true + 交给系统浏览器）。
        return IntPtr.Zero;
    }

    private static void OnScriptMessage(IntPtr userContentManager, IntPtr jsResult, IntPtr data)
    {
        try
        {
            WebKitWebViewCore? core = CoreOf(data);
            (string json, string? text) = ReadJsMessage(jsResult);
            WebViewLog.Debug($"JS→C# 消息：{json}");
            core?.Raise(() => core.MessageReceived?.Invoke(json));

            // 同时保留纯文本形态，供 TryGetWebMessageAsString 语义使用。
            _ = text;
        }
        catch (Exception ex)
        {
            WebViewLog.Error("script-message-received 处理异常", ex);
        }
    }

    private static void OnInternalScriptMessage(IntPtr userContentManager, IntPtr jsResult, IntPtr data)
    {
        try
        {
            WebKitWebViewCore? core = CoreOf(data);
            (string json, _) = ReadJsMessage(jsResult);
            if (json.Contains("DOMContentLoaded", StringComparison.Ordinal))
            {
                WebViewLog.Info("收到 DOMContentLoaded 回报（注入脚本）");
                core?.Raise(() => core.DocumentLoaded?.Invoke());
            }
            else
            {
                WebViewLog.Debug($"内部消息：{json}");
            }
        }
        catch (Exception ex)
        {
            WebViewLog.Error("内部消息处理异常", ex);
        }
    }

    /// <summary>把 JS 侧 postMessage 的载荷映射成 WebView2 的 JSON 语义。</summary>
    private static (string Json, string? Text) ReadJsMessage(IntPtr jsResult)
    {
        IntPtr value = WebKit.webkit_javascript_result_get_js_value(jsResult);
        if (value == IntPtr.Zero)
        {
            return ("null", null);
        }

        if (Jsc.jsc_value_is_string(value))
        {
            IntPtr strPtr = Jsc.jsc_value_to_string(value);
            string? text = strPtr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(strPtr);
            if (strPtr != IntPtr.Zero)
            {
                Glib.g_free(strPtr);
            }

            // WebView2：字符串载荷的 WebMessageAsJson 是带引号的 JSON 字符串。
            return (JsonSerializer.Serialize(text), text);
        }

        IntPtr jsonPtr = Jsc.jsc_value_to_json(value, 0);
        string json = jsonPtr == IntPtr.Zero ? "null" : Marshal.PtrToStringUTF8(jsonPtr) ?? "null";
        if (jsonPtr != IntPtr.Zero)
        {
            Glib.g_free(jsonPtr);
        }

        return (json, null);
    }

    // ------------------------------------------------ 资源嗅探（JSON 响应）

    private sealed class ResourceState(WebKitWebViewCore core, IntPtr resource, string uri)
    {
        public WebKitWebViewCore Core { get; } = core;
        public IntPtr Resource { get; } = resource;
        public string Uri { get; } = uri;
        public int Status { get; set; }
        public string Mime { get; set; } = "application/json";
        public ulong HandlerId { get; set; }
    }

    private static void OnResourceLoadStarted(IntPtr view, IntPtr resource, IntPtr request, IntPtr data)
    {
        try
        {
            WebKitWebViewCore? core = CoreOf(data);
            if (core is null || !core._enableResourceSniffing)
            {
                return;
            }

            string uri = Utf8(WebKit.webkit_web_resource_get_uri(resource));
            if (!uri.Contains(".json", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 信号参数是 borrowed reference；先加一份引用，等数据读完再放。
            IntPtr kept = Glib.g_object_ref(resource);
            var state = new ResourceState(core, kept, uri);
            GCHandle handle = GCHandle.Alloc(state);

            ulong id = Glib.g_signal_connect_data(
                kept,
                "finished",
                Marshal.GetFunctionPointerForDelegate(ResourceFinishedHandler),
                GCHandle.ToIntPtr(handle),
                IntPtr.Zero,
                0);
            state.HandlerId = id;
        }
        catch (Exception ex)
        {
            WebViewLog.Error("resource-load-started 处理异常", ex);
        }
    }

    private static void OnResourceFinished(IntPtr resource, IntPtr data)
    {
        ResourceState? state = null;
        GCHandle handle = default;
        try
        {
            if (data == IntPtr.Zero)
            {
                return;
            }

            handle = GCHandle.FromIntPtr(data);
            state = handle.Target as ResourceState;
            if (state is null)
            {
                return;
            }

            if (state.HandlerId != 0)
            {
                Glib.g_signal_handler_disconnect(resource, state.HandlerId);
                state.HandlerId = 0;
            }

            IntPtr response = WebKit.webkit_web_resource_get_response(resource);
            if (response != IntPtr.Zero)
            {
                state.Status = WebKit.webkit_uri_response_get_status_code(response);
                state.Mime = Utf8(WebKit.webkit_uri_response_get_mime_type(response));
            }

            WebKit.webkit_web_resource_get_data(
                resource,
                IntPtr.Zero,
                ResourceDataCallbackDelegate,
                GCHandle.ToIntPtr(handle));
        }
        catch (Exception ex)
        {
            WebViewLog.Error("resource finished 处理异常", ex);
            if (state is not null && state.Resource != IntPtr.Zero)
            {
                Glib.g_object_unref(state.Resource);
            }

            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
    }

    private static void OnResourceDataFinished(IntPtr resource, IntPtr res, IntPtr data)
    {
        ResourceState? state = null;
        GCHandle handle = default;
        try
        {
            if (data == IntPtr.Zero)
            {
                return;
            }

            handle = GCHandle.FromIntPtr(data);
            state = handle.Target as ResourceState;
            if (state is null)
            {
                return;
            }

            IntPtr dataPtr = WebKit.webkit_web_resource_get_data_finish(resource, res, out nuint length, out IntPtr error);
            byte[]? content = null;
            if (dataPtr != IntPtr.Zero && length > 0)
            {
                content = new byte[(int)length];
                Marshal.Copy(dataPtr, content, 0, (int)length);
                Glib.g_free(dataPtr);
            }
            else if (error != IntPtr.Zero)
            {
                WebViewLog.Warn($"读取资源数据失败：{ReadGErrorMessage(error)}");
            }

            FreeGError(error);

            int status = state.Status;
            string mime = state.Mime;
            string uri = state.Uri;
            byte[]? captured = content;
            WebKitWebViewCore core = state.Core;
            core.Raise(() => core.ResourceResponseReceived?.Invoke(uri, status, mime, () => captured));
        }
        catch (Exception ex)
        {
            WebViewLog.Error("resource data 处理异常", ex);
        }
        finally
        {
            if (state is not null && state.Resource != IntPtr.Zero)
            {
                Glib.g_object_unref(state.Resource);
            }

            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
    }

    // ------------------------------------------------------------------ 导航

    public void Navigate(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return;
        }

        string target = VirtualHostMapping.RewriteHttpsUri(uri) ?? uri;
        if (!string.Equals(target, uri, StringComparison.Ordinal))
        {
            WebViewLog.Info($"虚拟主机 URI 改写：{uri} -> {target}");
        }

        GtkRuntime.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                WebKit.webkit_web_view_load_uri(_view, target);
            }
            catch (Exception ex)
            {
                WebViewLog.Error($"导航失败：{target}", ex);
            }
        });
    }

    public void NavigateToString(string html, string? baseUri = null)
    {
        GtkRuntime.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                WebKit.webkit_web_view_load_html(_view, html, string.IsNullOrWhiteSpace(baseUri) ? null : baseUri);
            }
            catch (Exception ex)
            {
                WebViewLog.Error("NavigateToString 失败", ex);
            }
        });
    }

    public void Reload() => GtkRuntime.Post(() =>
    {
        if (!_disposed)
        {
            WebKit.webkit_web_view_reload(_view);
        }
    });

    public void Stop() => GtkRuntime.Post(() =>
    {
        if (!_disposed)
        {
            WebKit.webkit_web_view_stop_loading(_view);
        }
    });

    public bool CanGoBack() => SafeQuery(() => WebKit.webkit_web_view_can_go_back(_view));

    public bool CanGoForward() => SafeQuery(() => WebKit.webkit_web_view_can_go_forward(_view));

    public void GoBack() => GtkRuntime.Post(() =>
    {
        if (!_disposed)
        {
            WebKit.webkit_web_view_go_back(_view);
        }
    });

    public void GoForward() => GtkRuntime.Post(() =>
    {
        if (!_disposed)
        {
            WebKit.webkit_web_view_go_forward(_view);
        }
    });

    public void Focus() => GtkRuntime.Post(() =>
    {
        if (!_disposed)
        {
            Gtk.gtk_widget_grab_focus(_view);
        }
    });

    private bool SafeQuery(Func<bool> query)
    {
        try
        {
            return GtkRuntime.Invoke(query);
        }
        catch (Exception ex)
        {
            WebViewLog.Warn($"查询导航状态失败：{ex.Message}");
            return false;
        }
    }

    public void SetUserAgent(string? userAgent) => GtkRuntime.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        IntPtr settings = WebKit.webkit_web_view_get_settings(_view);
        if (settings == IntPtr.Zero)
        {
            return;
        }

        WebKit.webkit_settings_set_user_agent(settings, string.IsNullOrEmpty(userAgent) ? null : userAgent);
        WebViewLog.Info($"UserAgent = {userAgent ?? "<WebKit 默认>"}");
    });

    public void SetDarkMode(bool dark) => GtkRuntime.Post(() => SetGtkDarkPreference(dark));

    public void SetZoomFactor(double factor) => GtkRuntime.Post(() =>
    {
        if (!_disposed && factor > 0)
        {
            WebKit.webkit_web_view_set_zoom_level(_view, factor);
        }
    });

    public void SetBackgroundColor(byte r, byte g, byte b, byte a) => GtkRuntime.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        var color = new GdkRGBA { Red = r / 255.0, Green = g / 255.0, Blue = b / 255.0, Alpha = a / 255.0 };
        WebKit.webkit_web_view_set_background_color(_view, ref color);
    });

    /// <summary>嵌入模式移动/缩放 GdkWindow；独立窗口模式调整窗口大小。</summary>
    public void SetBounds(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        GtkRuntime.Post(() =>
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (IsEmbedded)
                {
                    Gtk.gtk_widget_set_size_request(_view, width, height);
                    SizeAllocate(_container, x, y, width, height);
                    IntPtr window = Gtk.gtk_widget_get_window(_container);
                    if (window != IntPtr.Zero)
                    {
                        Gdk.gdk_window_move_resize(window, x, y, width, height);
                    }

                    return;
                }

                Gtk.gtk_window_set_default_size(_container, width, height);
                Gtk.gtk_window_resize(_container, width, height);
            }
            catch (Exception ex)
            {
                WebViewLog.Warn($"调整 WebView 尺寸失败：{ex.Message}");
            }
        });
    }

    /// <summary>gtk_widget_size_allocate 的托管包装（GtkAllocation = 4 个 int，16 字节）。</summary>
    private static void SizeAllocate(IntPtr widget, int x, int y, int width, int height)
    {
        IntPtr allocation = Marshal.AllocHGlobal(16);
        try
        {
            Marshal.WriteInt32(allocation, 0, x);
            Marshal.WriteInt32(allocation, 4, y);
            Marshal.WriteInt32(allocation, 8, width);
            Marshal.WriteInt32(allocation, 12, height);
            Gtk.gtk_widget_size_allocate(widget, allocation);
        }
        finally
        {
            Marshal.FreeHGlobal(allocation);
        }
    }

    public void SetWindowTitle(string title) => GtkRuntime.Post(() =>
    {
        if (!_disposed && !IsEmbedded)
        {
            Gtk.gtk_window_set_title(_container, title);
        }
    });

    private void RefreshLocation()
    {
        string uri = Utf8(WebKit.webkit_web_view_get_uri(_view));
        if (string.Equals(uri, _currentUri, StringComparison.Ordinal))
        {
            return;
        }

        _currentUri = uri;
        Raise(() => SourceChanged?.Invoke(uri));
    }

    // -------------------------------------------------------------------- JS

    /// <summary>
    /// 等价 <c>CoreWebView2.ExecuteScriptAsync</c>：返回 JSON 文本（字符串结果带引号）。
    /// </summary>
    public async Task<string> ExecuteScriptAsync(string script) =>
        await ExecuteScriptAsync(script, ScriptWrapMode.Isolated).ConfigureAwait(false);

    public async Task<string> ExecuteScriptAsync(string script, ScriptWrapMode mode)
    {
        try
        {
            return await EvaluateJsonAsync(ScriptWrapper.Wrap(script, mode)).ConfigureAwait(false);
        }
        catch (JavaScriptException ex) when (mode == ScriptWrapMode.Isolated && IsEvalBlocked(ex))
        {
            // 页面 CSP 禁用 unsafe-eval 时退回原样注入（会失去 let 隔离，但至少能执行）。
            WebViewLog.Warn($"eval 被 CSP 拦截，改用原样注入：{ex.Message}");
            return await EvaluateJsonAsync(script).ConfigureAwait(false);
        }
    }

    private static bool IsEvalBlocked(JavaScriptException ex) =>
        ex.Message.Contains("unsafe-eval", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("Refused to evaluate", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("__lunarwardRun", StringComparison.Ordinal);

    private Task<string> EvaluateJsonAsync(string script)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        GtkRuntime.Post(() =>
        {
            if (_disposed)
            {
                tcs.TrySetException(new ObjectDisposedException(nameof(WebKitWebViewCore)));
                return;
            }

            try
            {
                GCHandle handle = GCHandle.Alloc(tcs);
                WebKit.webkit_web_view_evaluate_javascript(
                    _view,
                    script,
                    -1,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    EvalCallbackDelegate,
                    GCHandle.ToIntPtr(handle));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    /// <summary>执行脚本并返回字符串原值（结果不是字符串时返回 JSON）。</summary>
    public Task<string?> ExecuteScriptRawStringAsync(string script)
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        GtkRuntime.Post(() =>
        {
            if (_disposed)
            {
                tcs.TrySetException(new ObjectDisposedException(nameof(WebKitWebViewCore)));
                return;
            }

            try
            {
                GCHandle handle = GCHandle.Alloc(tcs);
                WebKit.webkit_web_view_evaluate_javascript(
                    _view, script, -1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, RawEvalCallbackDelegate, GCHandle.ToIntPtr(handle));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    private static void OnEvalFinished(IntPtr source, IntPtr res, IntPtr data)
    {
        GCHandle handle = GCHandle.FromIntPtr(data);
        try
        {
            if (handle.Target is not TaskCompletionSource<string> tcs)
            {
                return;
            }

            IntPtr value = WebKit.webkit_web_view_evaluate_javascript_finish(source, res, out IntPtr error);
            if (value == IntPtr.Zero)
            {
                string message = ReadGErrorMessage(error) ?? "脚本执行失败（无错误信息）";
                FreeGError(error);
                tcs.TrySetException(new JavaScriptException(message));
                return;
            }

            string json = "null";
            IntPtr jsonPtr = Jsc.jsc_value_to_json(value, 0);
            if (jsonPtr != IntPtr.Zero)
            {
                json = Marshal.PtrToStringUTF8(jsonPtr) ?? "null";
                Glib.g_free(jsonPtr);
            }

            Glib.g_object_unref(value);
            tcs.TrySetResult(json);
        }
        catch (Exception ex)
        {
            WebViewLog.Error("evaluate_javascript 回调异常", ex);
            (handle.Target as TaskCompletionSource<string>)?.TrySetException(ex);
        }
        finally
        {
            handle.Free();
        }
    }

    private static void OnRawEvalFinished(IntPtr source, IntPtr res, IntPtr data)
    {
        GCHandle handle = GCHandle.FromIntPtr(data);
        try
        {
            if (handle.Target is not TaskCompletionSource<string?> tcs)
            {
                return;
            }

            IntPtr value = WebKit.webkit_web_view_evaluate_javascript_finish(source, res, out IntPtr error);
            if (value == IntPtr.Zero)
            {
                string message = ReadGErrorMessage(error) ?? "脚本执行失败（无错误信息）";
                FreeGError(error);
                tcs.TrySetException(new JavaScriptException(message));
                return;
            }

            if (Jsc.jsc_value_is_string(value))
            {
                IntPtr strPtr = Jsc.jsc_value_to_string(value);
                string? text = strPtr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(strPtr);
                if (strPtr != IntPtr.Zero)
                {
                    Glib.g_free(strPtr);
                }

                Glib.g_object_unref(value);
                tcs.TrySetResult(text);
                return;
            }

            IntPtr jsonPtr = Jsc.jsc_value_to_json(value, 0);
            string? json = jsonPtr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(jsonPtr);
            if (jsonPtr != IntPtr.Zero)
            {
                Glib.g_free(jsonPtr);
            }

            Glib.g_object_unref(value);
            tcs.TrySetResult(json);
        }
        catch (Exception ex)
        {
            WebViewLog.Error("evaluate_javascript（raw）回调异常", ex);
            (handle.Target as TaskCompletionSource<string?>)?.TrySetException(ex);
        }
        finally
        {
            handle.Free();
        }
    }

    // ------------------------------------------------------------------ 截图

    /// <summary>等价 CapturePreviewAsync / Page.captureScreenshot，返回 PNG 字节。</summary>
    public Task<byte[]> CaptureSnapshotAsync(bool fullDocument)
    {
        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        GtkRuntime.Post(() =>
        {
            if (_disposed)
            {
                tcs.TrySetException(new ObjectDisposedException(nameof(WebKitWebViewCore)));
                return;
            }

            try
            {
                GCHandle handle = GCHandle.Alloc(tcs);
                WebKit.webkit_web_view_get_snapshot(
                    _view,
                    fullDocument ? WebKitSnapshotRegion.FullDocument : WebKitSnapshotRegion.Visible,
                    WebKitSnapshotOptions.None,
                    IntPtr.Zero,
                    SnapshotCallbackDelegate,
                    GCHandle.ToIntPtr(handle));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    private static void OnSnapshotFinished(IntPtr source, IntPtr res, IntPtr data)
    {
        GCHandle handle = GCHandle.FromIntPtr(data);
        string? tempFile = null;
        try
        {
            if (handle.Target is not TaskCompletionSource<byte[]> tcs)
            {
                return;
            }

            IntPtr surface = WebKit.webkit_web_view_get_snapshot_finish(source, res, out IntPtr error);
            if (surface == IntPtr.Zero)
            {
                string message = ReadGErrorMessage(error) ?? "截图失败";
                FreeGError(error);
                tcs.TrySetException(new InvalidOperationException(message));
                return;
            }

            try
            {
                tempFile = Path.Combine(Path.GetTempPath(), $"lunarward-webview-{Guid.NewGuid():N}.png");
                int status = Cairo.cairo_surface_write_to_png(surface, tempFile);
                if (status != Cairo.StatusSuccess)
                {
                    tcs.TrySetException(new InvalidOperationException($"cairo_surface_write_to_png 失败：{status}"));
                    return;
                }

                int width = Cairo.cairo_image_surface_get_width(surface);
                int height = Cairo.cairo_image_surface_get_height(surface);
                byte[] bytes = File.ReadAllBytes(tempFile);
                WebViewLog.Info($"截图完成：{width}x{height}，{bytes.Length} 字节");
                tcs.TrySetResult(bytes);
            }
            finally
            {
                Cairo.cairo_surface_destroy(surface);
            }
        }
        catch (Exception ex)
        {
            WebViewLog.Error("截图回调异常", ex);
            (handle.Target as TaskCompletionSource<byte[]>)?.TrySetException(ex);
        }
        finally
        {
            if (tempFile is not null)
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    // ignore
                }
            }

            handle.Free();
        }
    }

    // ----------------------------------------------------------------- Cookie

    public Task<IReadOnlyList<CookieSnapshot>> GetCookiesAsync(string uri)
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<CookieSnapshot>>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (string.IsNullOrWhiteSpace(uri))
        {
            tcs.SetResult([]);
            return tcs.Task;
        }

        GtkRuntime.Post(() =>
        {
            try
            {
                IntPtr manager = CookieManager();
                if (manager == IntPtr.Zero)
                {
                    tcs.TrySetResult([]);
                    return;
                }

                GCHandle handle = GCHandle.Alloc(tcs);
                WebKit.webkit_cookie_manager_get_cookies(manager, uri, IntPtr.Zero, CookiesCallbackDelegate, GCHandle.ToIntPtr(handle));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    public Task<IReadOnlyList<CookieSnapshot>> GetAllCookiesAsync()
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<CookieSnapshot>>(TaskCreationOptions.RunContinuationsAsynchronously);
        GtkRuntime.Post(() =>
        {
            try
            {
                IntPtr manager = CookieManager();
                if (manager == IntPtr.Zero)
                {
                    tcs.TrySetResult([]);
                    return;
                }

                GCHandle handle = GCHandle.Alloc(tcs);
                WebKit.webkit_cookie_manager_get_all_cookies(manager, IntPtr.Zero, AllCookiesCallbackDelegate, GCHandle.ToIntPtr(handle));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    private static void OnGetCookiesFinished(IntPtr source, IntPtr res, IntPtr data)
    {
        GCHandle handle = GCHandle.FromIntPtr(data);
        try
        {
            if (handle.Target is not TaskCompletionSource<IReadOnlyList<CookieSnapshot>> tcs)
            {
                return;
            }

            IntPtr list = WebKit.webkit_cookie_manager_get_cookies_finish(source, res, out IntPtr error);
            FreeGError(error);
            tcs.TrySetResult(ReadCookieList(list));
        }
        catch (Exception ex)
        {
            WebViewLog.Error("读取 Cookie 回调异常", ex);
            (handle.Target as TaskCompletionSource<IReadOnlyList<CookieSnapshot>>)?.TrySetException(ex);
        }
        finally
        {
            handle.Free();
        }
    }

    private static void OnGetAllCookiesFinished(IntPtr source, IntPtr res, IntPtr data)
    {
        GCHandle handle = GCHandle.FromIntPtr(data);
        try
        {
            if (handle.Target is not TaskCompletionSource<IReadOnlyList<CookieSnapshot>> tcs)
            {
                return;
            }

            IntPtr list = WebKit.webkit_cookie_manager_get_all_cookies_finish(source, res, out IntPtr error);
            FreeGError(error);
            tcs.TrySetResult(ReadCookieList(list));
        }
        catch (Exception ex)
        {
            WebViewLog.Error("读取全部 Cookie 回调异常", ex);
            (handle.Target as TaskCompletionSource<IReadOnlyList<CookieSnapshot>>)?.TrySetException(ex);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>遍历 GList&lt;SoupCookie*&gt; 并整体释放（transfer full）。</summary>
    private static List<CookieSnapshot> ReadCookieList(IntPtr list)
    {
        var cookies = new List<CookieSnapshot>();
        if (list == IntPtr.Zero)
        {
            return cookies;
        }

        try
        {
            IntPtr node = list;
            while (node != IntPtr.Zero)
            {
                // GList { gpointer data; GList *next; GList *prev; }
                IntPtr cookie = Marshal.ReadIntPtr(node, 0);
                if (cookie != IntPtr.Zero)
                {
                    cookies.Add(ReadCookie(cookie));
                }

                node = Marshal.ReadIntPtr(node, IntPtr.Size);
            }
        }
        finally
        {
            Glib.g_list_free_full(list, SoupCookieDestroyPointer);
        }

        return cookies;
    }

    private static CookieSnapshot ReadCookie(IntPtr cookie)
    {
        static string Str(IntPtr ptr) => ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;

        return new CookieSnapshot(
            Str(Soup.soup_cookie_get_name(cookie)),
            Str(Soup.soup_cookie_get_value(cookie)),
            Str(Soup.soup_cookie_get_domain(cookie)),
            Str(Soup.soup_cookie_get_path(cookie)),
            Soup.soup_cookie_get_secure(cookie),
            Soup.soup_cookie_get_http_only(cookie));
    }

    /// <summary>新增或覆盖一个 Cookie（等价 AddOrUpdateCookie）。</summary>
    public Task<bool> SetCookieAsync(CookieSnapshot cookie, SoupSameSitePolicy sameSite = SoupSameSitePolicy.Lax)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        GtkRuntime.Post(() =>
        {
            IntPtr native = IntPtr.Zero;
            try
            {
                IntPtr manager = CookieManager();
                if (manager == IntPtr.Zero)
                {
                    tcs.TrySetResult(false);
                    return;
                }

                native = Soup.soup_cookie_new(cookie.Name, cookie.Value, cookie.Domain, cookie.Path, -1);
                if (native == IntPtr.Zero)
                {
                    tcs.TrySetResult(false);
                    return;
                }

                Soup.soup_cookie_set_secure(native, cookie.Secure);
                Soup.soup_cookie_set_http_only(native, cookie.HttpOnly);
                Soup.soup_cookie_set_same_site_policy(native, sameSite);

                GCHandle handle = GCHandle.Alloc(new CookieWriteState(tcs, native));
                WebKit.webkit_cookie_manager_add_cookie(
                    manager, native, IntPtr.Zero, CookieWriteCallbackDelegate, GCHandle.ToIntPtr(handle));
            }
            catch (Exception ex)
            {
                if (native != IntPtr.Zero)
                {
                    Soup.soup_cookie_free(native);
                }

                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    public Task<bool> DeleteCookieAsync(CookieSnapshot cookie)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        GtkRuntime.Post(() =>
        {
            IntPtr native = IntPtr.Zero;
            try
            {
                IntPtr manager = CookieManager();
                if (manager == IntPtr.Zero)
                {
                    tcs.TrySetResult(false);
                    return;
                }

                native = Soup.soup_cookie_new(cookie.Name, cookie.Value, cookie.Domain, cookie.Path, -1);
                if (native == IntPtr.Zero)
                {
                    tcs.TrySetResult(false);
                    return;
                }

                GCHandle handle = GCHandle.Alloc(new CookieWriteState(tcs, native));
                WebKit.webkit_cookie_manager_delete_cookie(
                    manager, native, IntPtr.Zero, CookieWriteCallbackDelegate, GCHandle.ToIntPtr(handle));
            }
            catch (Exception ex)
            {
                if (native != IntPtr.Zero)
                {
                    Soup.soup_cookie_free(native);
                }

                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    public void ClearCookies() => GtkRuntime.Post(() =>
    {
        IntPtr manager = CookieManager();
        if (manager != IntPtr.Zero)
        {
            WebKit.webkit_cookie_manager_delete_all_cookies(manager);
            WebViewLog.Info("已清空全部 Cookie");
        }
    });

    private sealed class CookieWriteState(TaskCompletionSource<bool> tcs, IntPtr cookie)
    {
        public TaskCompletionSource<bool> Tcs { get; } = tcs;

        public IntPtr Cookie { get; } = cookie;
    }

    private static void OnCookieWriteFinished(IntPtr source, IntPtr res, IntPtr data)
    {
        GCHandle handle = GCHandle.FromIntPtr(data);
        CookieWriteState? state = null;
        try
        {
            state = handle.Target as CookieWriteState;
            if (state is null)
            {
                return;
            }

            bool ok = WebKit.webkit_cookie_manager_add_cookie_finish(source, res, out IntPtr error);
            if (!ok)
            {
                WebViewLog.Warn($"Cookie 写入返回失败：{ReadGErrorMessage(error)}");
            }

            FreeGError(error);
            state.Tcs.TrySetResult(ok);
        }
        catch (Exception ex)
        {
            WebViewLog.Error("Cookie 写入回调异常", ex);
            state?.Tcs.TrySetException(ex);
        }
        finally
        {
            if (state is not null && state.Cookie != IntPtr.Zero)
            {
                Soup.soup_cookie_free(state.Cookie);
            }

            handle.Free();
        }
    }

    private IntPtr CookieManager()
    {
        IntPtr context = WebKit.webkit_web_view_get_context(_view);
        return context == IntPtr.Zero ? IntPtr.Zero : WebKit.webkit_web_context_get_cookie_manager(context);
    }

    // --------------------------------------------- 自定义 scheme（虚拟主机）

    private static void EnsureUriSchemeRegistered(IntPtr view)
    {
        IntPtr context = WebKit.webkit_web_view_get_context(view);
        if (context == IntPtr.Zero)
        {
            return;
        }

        lock (SchemeGate)
        {
            if (!SchemeRegisteredContexts.Add(context))
            {
                return;
            }
        }

        IntPtr securityManager = WebKit.webkit_web_context_get_security_manager(context);
        if (securityManager != IntPtr.Zero)
        {
            WebKit.webkit_security_manager_register_uri_scheme_as_cors_enabled(securityManager, VirtualHostMapping.Scheme);
            WebKit.webkit_security_manager_register_uri_scheme_as_secure(securityManager, VirtualHostMapping.Scheme);
        }

        WebKit.webkit_web_context_register_uri_scheme(
            context, VirtualHostMapping.Scheme, UriSchemeDelegate, IntPtr.Zero, IntPtr.Zero);
        WebViewLog.Info($"已注册自定义 scheme：{VirtualHostMapping.Scheme}://（CORS 友好 + secure）");
    }

    private static void OnUriSchemeRequest(IntPtr request, IntPtr userData)
    {
        string uri = "?";
        try
        {
            uri = Utf8(WebKit.webkit_uri_scheme_request_get_uri(request));

            if (!VirtualHostMapping.TryParseUri(uri, out string host, out string path)
                || !VirtualHostMapping.TryResolveFile(host, path, out string file, out string contentType))
            {
                WebViewLog.Warn($"虚拟主机 404：{uri}");
                ServeBytes(request, "404 Not Found"u8.ToArray(), "text/plain; charset=utf-8");
                return;
            }

            byte[] bytes = File.ReadAllBytes(file);
            ServeBytes(request, bytes, contentType);
            WebViewLog.Info($"虚拟主机响应：{uri} -> {file}（{bytes.Length} 字节，{contentType}）");
        }
        catch (Exception ex)
        {
            WebViewLog.Error($"处理自定义 scheme 请求失败：{uri}", ex);
            ServeBytes(request, "500 Internal Error"u8.ToArray(), "text/plain; charset=utf-8");
        }
    }

    private static void ServeBytes(IntPtr request, byte[] bytes, string contentType)
    {
        IntPtr unmanaged = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, unmanaged, bytes.Length);

            // g_bytes_new 会复制一份，所以 GBytes 建好后立刻释放临时缓冲是安全的。
            IntPtr gbytes = Gio.g_bytes_new(unmanaged, (nuint)bytes.Length);
            try
            {
                IntPtr stream = Gio.g_memory_input_stream_new_from_bytes(gbytes);
                try
                {
                    WebKit.webkit_uri_scheme_request_finish(request, stream, bytes.Length, contentType);
                }
                finally
                {
                    if (stream != IntPtr.Zero)
                    {
                        Glib.g_object_unref(stream);
                    }
                }
            }
            finally
            {
                if (gbytes != IntPtr.Zero)
                {
                    Gio.g_bytes_unref(gbytes);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(unmanaged);
        }
    }

    // ------------------------------------------------------------------ 释放

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        void Teardown()
        {
            foreach ((IntPtr instance, ulong id) in _connections)
            {
                try
                {
                    Glib.g_signal_handler_disconnect(instance, id);
                }
                catch
                {
                    // ignore
                }
            }

            _connections.Clear();

            if (_container != IntPtr.Zero)
            {
                Gtk.gtk_widget_destroy(_container);
            }

            if (_userContentManager != IntPtr.Zero)
            {
                WebKit.webkit_user_content_manager_unregister_script_message_handler(_userContentManager, MessageHandlerName);
                WebKit.webkit_user_content_manager_unregister_script_message_handler(_userContentManager, InternalHandlerName);
                Glib.g_object_unref(_userContentManager);
            }
        }

        try
        {
            if (!GtkRuntime.IsInitialized)
            {
                // GTK 没起来，原生对象也不存在，直接回收句柄。
                if (_selfHandle.IsAllocated)
                {
                    _selfHandle.Free();
                }

                return;
            }

            if (GtkRuntime.IsGtkThread)
            {
                Teardown();
            }
            else
            {
                GtkRuntime.InvokeAsync(Teardown).Wait(TimeSpan.FromSeconds(10));
            }
        }
        catch (Exception ex)
        {
            WebViewLog.Error("销毁 WebView 失败", ex);
        }
        finally
        {
            if (_selfHandle.IsAllocated)
            {
                _selfHandle.Free();
            }
        }
    }
}
