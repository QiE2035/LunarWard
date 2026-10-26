using System.Diagnostics;
using System.Runtime.InteropServices;
using LunarWard.Frameworks.WebView.Interop;

namespace LunarWard.Frameworks.WebView;

/// <summary>
/// WebView 子系统的日志出口。控件本身不直接依赖 Serilog（自检程序可以脱离主程序运行），
/// 主程序启动时把 <see cref="Sink"/> 接到自己的 logger 上即可。
/// </summary>
public static class WebViewLog
{
    private static readonly object Gate = new();

    /// <summary>额外的日志接收者。GUI 里接 Serilog，自检程序里留空走 Trace/Console。</summary>
    public static event Action<string>? Sink;

    public static void Info(string message) => Write("INFO ", message, null);

    public static void Warn(string message) => Write("WARN ", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    public static void Debug(string message) => Write("DEBUG", message, null);

    private static void Write(string level, string message, Exception? ex)
    {
        string line = $"[webview] {level} {message}";
        if (ex is not null)
        {
            line += $" :: {ex.GetType().Name}: {ex.Message}";
        }

        Trace.WriteLine(line);
        Console.Error.WriteLine(line);

        Action<string>? sink = Sink;
        if (sink is null)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                sink(line);
            }
        }
        catch
        {
            // 日志失败不能影响浏览流程
        }
    }
}

/// <summary>
/// 运行时库探测。<see cref="IsAvailable"/> 为 false 时控件仍可构造，但不会创建原生视图，
/// 只会触发 <see cref="WebView.RenderFailed"/>，窗口可以据此显示错误提示（等价上游
/// 的 “WebView2 组件初始化失败” 分支）。
/// </summary>
public static class WebViewAvailability
{
    private static readonly Lazy<ProbeResult> LazyProbe = new(Probe, LazyThreadSafetyMode.ExecutionAndPublication);

    public static ProbeResult ProbeResult => LazyProbe.Value;

    public static bool IsAvailable => LazyProbe.Value.IsAvailable;

    /// <summary>如 "2.48.3"；库缺失时为 null。</summary>
    public static string? WebKitVersion => LazyProbe.Value.WebKitVersion;

    /// <summary>如 "3.24.38"；库缺失时为 null。</summary>
    public static string? GtkVersion => LazyProbe.Value.GtkVersion;

    public static string MissingLibrariesMessage => LazyProbe.Value.MissingMessage;

    /// <summary>等价 CoreWebView2Environment.GetAvailableBrowserVersionString()（缺失时抛异常）。</summary>
    public static string GetBrowserVersionString() => LazyProbe.Value.GetBrowserVersionString();

    /// <summary>重新探测（自检程序在设置过 GDK_BACKEND 后调用）。</summary>
    public static ProbeResult Reprogram() => Probe();

    private static ProbeResult Probe()
    {
        var missing = new List<string>();
        var present = new Dictionary<string, IntPtr>(StringComparer.Ordinal);

        foreach (string soname in Sonames.Required.Concat(Sonames.Optional))
        {
            if (NativeLibrary.TryLoad(soname, out IntPtr handle))
            {
                present[soname] = handle;
            }
            else
            {
                missing.Add(soname);
            }
        }

        string? webkitVersion = null;
        string? gtkVersion = null;

        if (!missing.Contains(Sonames.WebKit))
        {
            try
            {
                webkitVersion = $"{WebKit.webkit_get_major_version()}.{WebKit.webkit_get_minor_version()}.{WebKit.webkit_get_micro_version()}";
            }
            catch (Exception ex)
            {
                WebViewLog.Warn($"读取 WebKitGTK 版本失败：{ex.Message}");
            }
        }

        if (!missing.Contains(Sonames.Gtk))
        {
            try
            {
                gtkVersion = $"{Gtk.gtk_get_major_version()}.{Gtk.gtk_get_minor_version()}.{Gtk.gtk_get_micro_version()}";
            }
            catch (Exception ex)
            {
                WebViewLog.Warn($"读取 GTK 版本失败：{ex.Message}");
            }
        }

        bool available = !missing.Contains(Sonames.WebKit)
                         && !missing.Contains(Sonames.Gtk)
                         && !missing.Contains(Sonames.Gdk)
                         && !missing.Contains(Sonames.GObject)
                         && !missing.Contains(Sonames.Glib);

        // 加载成功的 handle 保持不释放：它们本来就要在整个进程生命周期内被 P/Invoke 使用。
        return new ProbeResult(available, webkitVersion, gtkVersion, missing, Sonames.InstallHint);
    }
}

/// <summary>探测结果。</summary>
public sealed class ProbeResult(
    bool isAvailable,
    string? webkitVersion,
    string? gtkVersion,
    IReadOnlyList<string> missingLibraries,
    string installHint)
{
    public bool IsAvailable { get; } = isAvailable;

    public string? WebKitVersion { get; } = webkitVersion;

    public string? GtkVersion { get; } = gtkVersion;

    public IReadOnlyList<string> MissingLibraries { get; } = missingLibraries;

    public string InstallHint { get; } = installHint;

    public string MissingMessage => MissingLibraries.Count == 0
        ? string.Empty
        : $"缺少原生库：{string.Join(", ", MissingLibraries)}\n{InstallHint}";

    /// <summary>等价 CoreWebView2Environment.GetAvailableBrowserVersionString()：缺失时抛异常。</summary>
    public string GetBrowserVersionString()
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException($"WebKitGTK 不可用。{MissingMessage}");
        }

        return WebKitVersion ?? "unknown";
    }

    public override string ToString() =>
        $"WebKitGTK={WebKitVersion ?? "<missing>"} GTK={GtkVersion ?? "<missing>"} available={IsAvailable} missing=[{string.Join(",", MissingLibraries)}]";
}
