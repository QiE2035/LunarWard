using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using LunarWard.Frameworks.WebView.Interop;

namespace LunarWard.Frameworks.WebView;

/// <summary>嵌入策略。</summary>
public enum WebViewEmbeddingMode
{
    /// <summary>能嵌就嵌（X11 XID），否则降级成独立 GTK 窗口。</summary>
    Auto = 0,

    /// <summary>强制独立 GTK 窗口。</summary>
    StandaloneWindow = 1,
}

/// <summary>
/// WebKitGTK 版 WebView 控件，用来替代上游的 <c>Microsoft.Web.WebView2.Wpf.WebView2</c>。
///
/// 实现方式：
/// <list type="bullet">
///   <item>GTK3 + libwebkit2gtk-4.1 通过 P/Invoke 调用，跑在专属的 "LunarWard-GTK" 线程上；</item>
///   <item>X11 会话：<see cref="CreateNativeControlCore"/> 建一个 X11 子窗口当 XEmbed socket，
///   再在该 XID 上用 <c>gtk_plug_new</c> 嵌进去，所以控件行为和普通 Avalonia 控件一致；</item>
///   <item>Wayland / 无 X11：GDK 无法把原生子窗口塞进另一个进程/另一个 toolkit 的窗口，
///   自动降级成独立 GTK 窗口，并在日志与 <see cref="StatusMessage"/> 里说明。</item>
/// </list>
/// 用法与上游一致：
/// <code>
/// await webView.EnsureCoreWebView2Async();
/// webView.CoreWebView2.NavigationCompleted += ...;
/// webView.Source = new Uri("https://example.com");
/// </code>
/// </summary>
public sealed class WebView : NativeControlHost
{
    /// <summary>源码绑定属性（XAML 里 <c>Source="https://..."</c> 可用）。</summary>
    public static readonly StyledProperty<Uri?> SourceProperty =
        AvaloniaProperty.Register<WebView, Uri?>(nameof(Source));

    private readonly object _initGate = new();
    private readonly TaskCompletionSource<ulong> _xidSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private WebKitWebViewCore? _nativeCore;
    private CoreWebView2? _coreWebView2;
    private Task<CoreWebView2>? _initTask;
    private ulong _xid;
    private IntPtr _display;
    private bool _x11DisplayOpened;
    private Uri? _pendingSource;
    private string? _logSink;

    public WebView()
    {
        // Avalonia 的 UI 主题决定 Auto 配色；原生侧最终仍以 GTK 主题为准。
        WebViewLog.Sink += OnLog;
        SizeChanged += (_, _) => UpdateNativeBounds();
    }

    /// <summary>嵌入策略；默认 Auto。</summary>
    public WebViewEmbeddingMode EmbeddingMode { get; set; } = WebViewEmbeddingMode.Auto;

    /// <summary>最近一次初始化/降级的状态说明，供宿主界面展示。</summary>
    public string StatusMessage { get; private set; } = string.Empty;

    /// <summary>是否成功嵌进 Avalonia 窗口（false 表示走独立 GTK 窗口降级）。</summary>
    public bool IsEmbedded => _nativeCore?.IsEmbedded ?? false;

    /// <summary>WebKitGTK 是否可用（等价「WebView2 组件是否已安装」）。</summary>
    public static bool IsWebViewAvailable => WebViewAvailability.IsAvailable;

    /// <summary>缺失依赖时的说明 + 安装命令。</summary>
    public static string MissingDependenciesMessage => WebViewAvailability.MissingLibrariesMessage;

    /// <summary>对应 CoreWebView2；初始化完成前为 null。</summary>
    public CoreWebView2? CoreWebView2 => _coreWebView2;

    /// <summary>控件源码（等价 WebView2.Source）。</summary>
    public Uri? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    internal WebKitWebViewCore? NativeCore => _nativeCore;

    /// <summary>系统是否深色主题。</summary>
    public bool IsSystemDarkTheme
    {
        get
        {
            try
            {
                return Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
            }
            catch
            {
                return false;
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            ApplySource(change.GetNewValue<Uri?>());
        }
    }

    private void ApplySource(Uri? uri)
    {
        if (uri is null)
        {
            return;
        }

        if (_nativeCore is null)
        {
            _pendingSource = uri;
            return;
        }

        Navigate(uri.ToString());
    }

    /// <summary>等价 CoreWebView2Environment.EnsureCoreWebView2Async 的控件版入口。</summary>
    public Task<CoreWebView2> EnsureCoreWebView2Async()
    {
        lock (_initGate)
        {
            return _initTask ??= InitializeAsync();
        }
    }

    private async Task<CoreWebView2> InitializeAsync()
    {
        if (!WebViewAvailability.IsAvailable)
        {
            StatusMessage = WebViewAvailability.MissingLibrariesMessage;
            WebViewLog.Error($"WebKitGTK 不可用，WebView 无法初始化。{StatusMessage}");
            throw new InvalidOperationException(StatusMessage);
        }

        // 先给 Avalonia 一点时间吐出 X11 子窗口；未挂到可视树就按独立窗口处理。
        ulong xid = await WaitForXidAsync(TimeSpan.FromMilliseconds(1500)).ConfigureAwait(true);
        bool wantEmbed = EmbeddingMode != WebViewEmbeddingMode.StandaloneWindow && xid != 0;

        string error = string.Empty;
        bool started = await Task.Run(() => GtkRuntime.EnsureStarted(wantEmbed, out error)).ConfigureAwait(true);
        if (!started)
        {
            StatusMessage = string.IsNullOrEmpty(error) ? "GTK 初始化失败" : error;
            WebViewLog.Error($"GTK 初始化失败：{error}");
            throw new InvalidOperationException(StatusMessage);
        }

        if (wantEmbed && GtkRuntime.Backend != "x11")
        {
            WebViewLog.Warn($"GDK 后端是 {GtkRuntime.Backend}，无法 XEmbed，改用独立 GTK 窗口");
            StatusMessage = $"当前 GDK 后端为 {GtkRuntime.Backend}，无法嵌入，已改用独立 GTK 窗口。"
                            + "如需嵌入请设置 GDK_BACKEND=x11 或环境变量 LUNARWARD_WEBVIEW_GDK_BACKEND=x11。";
            xid = 0;
        }

        double scaling = 1.0;
        int width = Math.Max(320, (int)(Bounds.Width * scaling));
        int height = Math.Max(240, (int)(Bounds.Height * scaling));

        var options = new WebViewCreateOptions(
            xid,
            "LunarWard WebView",
            width,
            height,
            UserAgent: null,
            DarkMode: IsSystemDarkTheme,
            DeveloperExtras: true);

        WebKitWebViewCore core = await GtkRuntime.InvokeAsync(() => WebKitWebViewCore.Create(options)).ConfigureAwait(true);
        _nativeCore = core;

        if (core.IsEmbedded)
        {
            StatusMessage = $"已通过 XEmbed 嵌入（socket=0x{xid:X}）";
            WebViewLog.Info(StatusMessage);
        }
        else
        {
            StatusMessage = "GDK 后端不支持 XID 嵌入，WebView 运行在独立 GTK 窗口中（Wayland 降级路径）";
            WebViewLog.Warn(StatusMessage);
        }

        core.SetWindowTitle("LunarWard");
        core.Dispatch = PostToUi;

        _coreWebView2 = new CoreWebView2(this, core, PostToUi);
        UpdateNativeBounds();

        if (_pendingSource is not null)
        {
            Uri pending = _pendingSource;
            _pendingSource = null;
            Navigate(pending.ToString());
        }

        return _coreWebView2;
    }

    private async Task<ulong> WaitForXidAsync(TimeSpan timeout)
    {
        if (_xid != 0)
        {
            return _xid;
        }

        // NativeControlHost 只有在挂到可视树、且平台支持原生子控件时才会创建窗口。
        if (VisualRoot is null)
        {
            return 0;
        }

        Task completed = await Task.WhenAny(_xidSource.Task, Task.Delay(timeout)).ConfigureAwait(true);
        return completed == _xidSource.Task ? await _xidSource.Task.ConfigureAwait(true) : 0;
    }

    /// <summary>等价 CoreWebView2.Navigate。</summary>
    public void Navigate(string uri)
    {
        if (_nativeCore is null)
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed))
            {
                _pendingSource = parsed;
            }

            WebViewLog.Warn("WebView 尚未初始化，导航被挂起");
            return;
        }

        _nativeCore.Navigate(uri);
    }

    /// <summary>等价 CoreWebView2.NavigateToString。</summary>
    public void NavigateToString(string html) => _nativeCore?.NavigateToString(html);

    internal void ApplyUserAgent(string? userAgent) => _nativeCore?.SetUserAgent(userAgent);

    internal void ApplyDarkMode(bool dark) => _nativeCore?.SetDarkMode(dark);

    /// <summary>把回调切回 Avalonia UI 线程。</summary>
    internal void PostToUi(Action action)
    {
        try
        {
            Dispatcher.UIThread.Post(action);
        }
        catch (Exception ex)
        {
            WebViewLog.Error("切回 UI 线程失败", ex);
            action();
        }
    }

    // -------------------------------------------------------------- 原生子窗口

    /// <summary>
    /// 建 X11 子窗口当 XEmbed socket。
    /// Avalonia 12 的 <c>NativeControlHost</c> 不接受 <c>null</c> 返回值（会在
    /// <c>X11NativeControlHost.Attachment</c> 里 NRE），所以降级时也返回一个占位句柄，
    /// 真正的画面在独立 GTK 窗口里。
    /// </summary>
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        string descriptor = parent.HandleDescriptor ?? string.Empty;
        if (!IsX11Handle(descriptor))
        {
            WebViewLog.Warn($"父句柄类型 '{descriptor}' 不是 X11 窗口，WebView 走独立 GTK 窗口降级路径");
            StatusMessage = $"Avalonia 当前平台句柄是 '{descriptor}'，不是 X11 窗口，无法嵌入。";
            _xidSource.TrySetResult(0);
            return new PlatformHandle(IntPtr.Zero, "XWindow");
        }

        try
        {
            _display = X11.XOpenDisplay(IntPtr.Zero);
            if (_display == IntPtr.Zero)
            {
                WebViewLog.Error("XOpenDisplay 失败（DISPLAY 不可用）");
                _xidSource.TrySetResult(0);
                return new PlatformHandle(IntPtr.Zero, "XWindow");
            }

            _x11DisplayOpened = true;

            uint width = (uint)Math.Max(1, Bounds.Width);
            uint height = (uint)Math.Max(1, Bounds.Height);

            IntPtr child = X11.XCreateWindow(
                _display,
                parent.Handle,
                0,
                0,
                width,
                height,
                0,
                depth: 0,
                windowClass: X11.InputOutput,
                visual: IntPtr.Zero,
                valueMask: 0,
                attributes: IntPtr.Zero);

            if (child == IntPtr.Zero)
            {
                WebViewLog.Error("XCreateWindow 返回 0");
                _xidSource.TrySetResult(0);
                return new PlatformHandle(IntPtr.Zero, "XWindow");
            }

            X11.XMapWindow(_display, child);
            X11.XFlush(_display);

            _xid = (ulong)child.ToInt64();
            WebViewLog.Info($"已创建 X11 子窗口（XEmbed socket）XID=0x{_xid:X}，尺寸 {width}x{height}");

            // EmbeddingMode=StandaloneWindow 时保留 socket 但不交给 GTK，画面走独立窗口。
            _xidSource.TrySetResult(EmbeddingMode == WebViewEmbeddingMode.StandaloneWindow ? 0 : _xid);
            return new PlatformHandle(child, "XWindow");
        }
        catch (Exception ex)
        {
            WebViewLog.Error("创建 X11 子窗口失败", ex);
            _xidSource.TrySetResult(0);
            return new PlatformHandle(IntPtr.Zero, "XWindow");
        }
    }

    private static bool IsX11Handle(string descriptor) =>
        descriptor.Contains("X11", StringComparison.OrdinalIgnoreCase)
        || descriptor.Contains("XWindow", StringComparison.OrdinalIgnoreCase)
        || descriptor.Contains("XID", StringComparison.OrdinalIgnoreCase);

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        try
        {
            _nativeCore?.Dispose();
            _nativeCore = null;
            _coreWebView2 = null;
        }
        catch (Exception ex)
        {
            WebViewLog.Error("销毁 WebView 原生控件失败", ex);
        }

        if (_x11DisplayOpened && _display != IntPtr.Zero)
        {
            try
            {
                if (control.Handle != IntPtr.Zero)
                {
                    X11.XDestroyWindow(_display, control.Handle);
                }

                X11.XCloseDisplay(_display);
            }
            catch (Exception ex)
            {
                WebViewLog.Warn($"清理 X11 资源失败：{ex.Message}");
            }
            finally
            {
                _display = IntPtr.Zero;
                _x11DisplayOpened = false;
                _xid = 0;
            }
        }

        WebViewLog.Sink -= OnLog;
    }

    private void UpdateNativeBounds()
    {
        if (_nativeCore is null)
        {
            return;
        }

        int width = (int)Bounds.Width;
        int height = (int)Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _nativeCore.SetBounds(0, 0, width, height);

        if (_x11DisplayOpened && _display != IntPtr.Zero && _xid != 0)
        {
            X11.XMoveResizeWindow(_display, new IntPtr((long)_xid), 0, 0, (uint)width, (uint)height);
            X11.XFlush(_display);
        }
    }

    private void OnLog(string message)
    {
        _logSink = message;
        StatusMessage = message;
    }

    /// <summary>最近一条 WebView 日志（调试用）。</summary>
    public string? LastLogLine => _logSink;
}
