using System.Text;
using System.Text.Json;
using LunarWard.Frameworks.WebView;
using LunarWard.Frameworks.WebView.Interop;

namespace LunarWard.Frameworks.WebView.SelfTest;

/// <summary>
/// WebKitGTK WebView 自检：真实加载 HTML → 执行 JS → JS 通过
/// window.chrome.webview.postMessage 把消息回传给 C# → 引擎截图落盘。
///
/// 全部判据都打印到 stdout（PASS/FAIL），运行方式见 SelfTest.csproj 头部注释。
/// </summary>
internal static class Program
{
    private static readonly List<string> Failures = [];
    private static readonly StringBuilder Evidence = new();

    private static int Main(string[] args)
    {
        // WebViewLog 本身已经写到 Console.Error，这里只补充证据文件，避免重复行。
        WebViewLog.Sink += line => Evidence.AppendLine(line);

        Console.WriteLine("=== LunarWard WebView (WebKitGTK) 自检 ===");

        ProbeResult probe = WebViewAvailability.ProbeResult;
        Console.WriteLine($"[probe] {probe}");
        if (!probe.IsAvailable)
        {
            Console.Error.WriteLine(probe.MissingMessage);
            return 2;
        }

        if (args.Contains("--probe"))
        {
            PrintProbe();
            return 0;
        }

        bool wayland = args.Contains("--wayland");
        bool xembed = args.Contains("--xembed");
        bool embed = TryReadXid(args, out ulong xid) || xembed;

        string outDir = ReadArgument(args, "--out")
                        ?? Path.Combine(FindRepoRoot(), "artifacts", "webview-selftest");
        Directory.CreateDirectory(outDir);

        try
        {
            return RunAsync(wayland, embed, xid, xembed, outDir).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[fatal] {ex}");
            Fail("未捕获异常：" + ex.Message);
            return 1;
        }
        finally
        {
            WriteLog(outDir);
        }
    }

    private static async Task<int> RunAsync(bool wayland, bool embed, ulong xid, bool xembed, string outDir)
    {
        // GDK 后端在 gtk_init 时确定；这里显式指定，便于两种路径都可实测。
        Environment.SetEnvironmentVariable("LUNARWARD_WEBVIEW_GDK_BACKEND", wayland ? "wayland" : "x11");

        string error = string.Empty;
        bool started = await Task.Run(() => GtkRuntime.EnsureStarted(!wayland, out error));
        if (!started)
        {
            Fail($"GTK 初始化失败：{error}");
            return 1;
        }

        IntPtr socketDisplay = IntPtr.Zero;
        IntPtr socketWindow = IntPtr.Zero;
        if (xembed)
        {
            // 模拟 Avalonia NativeControlHost 的行为：自己建一个 X11 子窗口当 XEmbed socket。
            socketDisplay = X11.XOpenDisplay(IntPtr.Zero);
            if (socketDisplay == IntPtr.Zero)
            {
                Fail("XOpenDisplay 失败，无法创建 XEmbed socket");
                return 1;
            }

            IntPtr root = X11.XDefaultRootWindow(socketDisplay);
            socketWindow = X11.XCreateWindow(
                socketDisplay, root, 40, 40, 900, 600, 0, 0, X11.InputOutput, IntPtr.Zero, 0, IntPtr.Zero);
            if (socketWindow == IntPtr.Zero)
            {
                Fail("XCreateWindow 失败，无法创建 XEmbed socket");
                return 1;
            }

            X11.XMapWindow(socketDisplay, socketWindow);
            X11.XFlush(socketDisplay);
            xid = (ulong)socketWindow.ToInt64();
            Console.WriteLine($"[xembed] 自建 socket 窗口 XID=0x{xid:X}");
        }

        Console.WriteLine($"[env] GDK 后端 = {GtkRuntime.Backend}，嵌入模式 = {(embed ? $"XID 0x{xid:X}" : "独立 GTK 窗口")}");
        Evidence.AppendLine($"[env] backend={GtkRuntime.Backend} embed={(embed ? xid.ToString("X") : "standalone")}");

        var options = new WebViewCreateOptions(
            ParentXid: embed ? xid : 0,
            WindowTitle: "LunarWard WebView 自检",
            Width: 900,
            Height: 600,
            UserAgent: "LunarWardSelfTest/1.0",
            DarkMode: true,
            DeveloperExtras: true);

        WebKitWebViewCore core = await GtkRuntime.InvokeAsync(() => WebKitWebViewCore.Create(options));

        if (xembed)
        {
            bool plugged = await GtkRuntime.InvokeAsync(() => Gtk.gtk_plug_get_embedded(core.Container));
            Console.WriteLine($"[xembed] GtkPlug 已嵌入 = {plugged}，plug 窗口 = 0x{await GtkRuntime.InvokeAsync(() => Gtk.gtk_plug_get_id(core.Container)):X}");
            Check(core.IsEmbedded && plugged, "GtkPlug 成功嵌入外部 X11 socket（XEmbed 路径，Avalonia 走同一条路）");
        }

        var messageTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var titleTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var documentLoadedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var navigationTcs = new TaskCompletionSource<(string Uri, bool Success)>(TaskCreationOptions.RunContinuationsAsynchronously);

        core.MessageReceived += json =>
        {
            Console.WriteLine($"[event] WebMessageReceived = {json}");
            messageTcs.TrySetResult(json);
        };
        core.TitleChanged += title => titleTcs.TrySetResult(title);
        core.DocumentLoaded += () =>
        {
            Console.WriteLine("[event] DOMContentLoaded");
            documentLoadedTcs.TrySetResult(true);
        };
        core.NavigationCompleted += (uri, success) =>
        {
            Console.WriteLine($"[event] NavigationCompleted uri={uri} success={success}");
            navigationTcs.TrySetResult((uri, success));
        };

        string html = BuildHtml();
        core.NavigateToString(html, "lunarward://selftest/");

        // --- 1) 导航完成 ---
        (string Uri, bool Success) nav = await WithTimeout(navigationTcs.Task, TimeSpan.FromSeconds(30), "NavigationCompleted");
        Check(nav.Success, $"NavigateToString 导航完成（uri={nav.Uri}）");

        // --- 2) DOMContentLoaded（注入脚本回报，WebKitGTK 4.1 没有等价信号）---
        bool domLoaded = await WithTimeout(documentLoadedTcs.Task, TimeSpan.FromSeconds(30), "DOMContentLoaded");
        Check(domLoaded, "DOMContentLoaded 通过注入脚本回报成功");

        // --- 3) JS → C# 消息回传 ---
        string messageJson = await WithTimeout(messageTcs.Task, TimeSpan.FromSeconds(30), "WebMessageReceived");
        Check(messageJson.Contains("hello", StringComparison.Ordinal) && messageJson.Contains("world", StringComparison.Ordinal),
            $"JS→C# 消息回传内容正确：{messageJson}");

        // --- 4) 执行 JS 并取回值（JSON 语义与 WebView2 一致）---
        string titleJson = await core.ExecuteScriptAsync("document.title");
        Console.WriteLine($"[js] document.title => {titleJson}");
        Check(titleJson == "\"LunarWard-WebView-SelfTest\"", "ExecuteScriptAsync 返回 JSON 字符串");

        string mathJson = await core.ExecuteScriptAsync("1 + 1");
        Console.WriteLine($"[js] 1 + 1 => {mathJson}");
        Check(mathJson == "2", "ExecuteScriptAsync 返回数值");

        string objectJson = await core.ExecuteScriptAsync("({ a: 1, b: 'two' })");
        Console.WriteLine($"[js] 对象 => {objectJson}");
        Check(objectJson.Contains("\"a\":1", StringComparison.Ordinal) && objectJson.Contains("\"b\":\"two\"", StringComparison.Ordinal),
            "ExecuteScriptAsync 返回对象 JSON");

        // --- 5) 顶层 let 隔离：连续两次执行同一段 let 脚本，第二次不能抛重声明错误 ---
        string first = await core.ExecuteScriptAsync("let __lwProbe = 41; __lwProbe + 1");
        string second = await core.ExecuteScriptAsync("let __lwProbe = 41; __lwProbe + 1");
        Console.WriteLine($"[js] let 隔离：first={first} second={second}");
        Check(first == "42" && second == "42", "同一段含顶层 let 的脚本可重复执行（上游 DOMContentLoaded 里会重复跑）");

        // 视口尺寸（嵌入模式下若 socket 没协商好，这里能立刻看出来）
        (int ViewWidth, int ViewHeight, int PlugWidth) sizes = await GtkRuntime.InvokeAsync(() => (
            Gtk.gtk_widget_get_allocated_width(core.View),
            Gtk.gtk_widget_get_allocated_height(core.View),
            Gtk.gtk_widget_get_allocated_width(core.Container)));
        Console.WriteLine($"[size] webview 分配尺寸 = {sizes.ViewWidth}x{sizes.ViewHeight}，容器宽度 = {sizes.PlugWidth}");
        Check(sizes.ViewWidth >= 600 && sizes.ViewHeight >= 400, $"WebView 视口尺寸合理（{sizes.ViewWidth}x{sizes.ViewHeight}）");

        // --- 6) 引擎截图落盘 ---
        byte[] png = await core.CaptureSnapshotAsync(fullDocument: true);
        string tag = xembed ? "xembed" : wayland ? "wayland" : "x11";
        string pngPath = Path.Combine(outDir, $"webview-selftest-{tag}.png");
        await File.WriteAllBytesAsync(pngPath, png);
        Console.WriteLine($"[snapshot] {pngPath}（{png.Length} 字节）");
        Check(png.Length > 1000 && png[0] == 0x89 && png[1] == 0x50, "截图返回合法 PNG");

        byte[] visible = await core.CaptureSnapshotAsync(fullDocument: false);
        string visiblePath = Path.Combine(outDir, $"webview-selftest-{tag}-viewport.png");
        await File.WriteAllBytesAsync(visiblePath, visible);
        Console.WriteLine($"[snapshot] {visiblePath}（{visible.Length} 字节）");

        // --- 7) Cookie 往返（LoginPage / BBSWebBridge 登录流程依赖）---
        bool cookieSet = await core.SetCookieAsync(new CookieSnapshot("lw_test", "1", "lunarward.local", "/", false, false));
        IReadOnlyList<CookieSnapshot> cookies = await core.GetAllCookiesAsync();
        Console.WriteLine($"[cookie] 写入={cookieSet} 现有={cookies.Count} 命中={cookies.Any(c => c.Name == "lw_test")}");
        Check(cookieSet && cookies.Any(c => c.Name == "lw_test"), "Cookie 写入 + 读回成功");

        // --- 8) 独立窗口 / 嵌入模式结论 ---
        Console.WriteLine($"[mode] IsEmbedded={core.IsEmbedded}，窗口/插件容器句柄=0x{core.Container:X}");
        Evidence.AppendLine($"embedded={core.IsEmbedded} backend={GtkRuntime.Backend}");

        core.Dispose();
        GtkRuntime.Shutdown();

        if (socketWindow != IntPtr.Zero)
        {
            X11.XDestroyWindow(socketDisplay, socketWindow);
            X11.XCloseDisplay(socketDisplay);
        }

        Console.WriteLine();
        if (Failures.Count == 0)
        {
            Console.WriteLine($"=== 全部通过（{outDir}）===");
            return 0;
        }

        Console.WriteLine($"=== 失败 {Failures.Count} 项 ===");
        foreach (string failure in Failures)
        {
            Console.WriteLine("  - " + failure);
        }

        return 1;
    }

    /// <summary>自检页面：标题 + 一段脚本，脚本在 DOMContentLoaded 后回传消息。</summary>
    private static string BuildHtml() => """
        <!doctype html>
        <html>
        <head>
          <meta charset="utf-8">
          <title>LunarWard-WebView-SelfTest</title>
          <style>
            body { font-family: sans-serif; background:#1b1b1f; color:#e8e8ea; padding:32px; }
            h1 { color:#ffc53d; }
            code { background:#2a2a31; padding:2px 6px; border-radius:4px; }
          </style>
        </head>
        <body>
          <h1>WebKitGTK 自检页</h1>
          <p>这个页面由 <code>NavigateToString</code> 载入，下面这段脚本会把消息回传给 C#。</p>
          <ul id="list"></ul>
          <script>
            document.addEventListener('DOMContentLoaded', function () {
              var list = document.getElementById('list');
              ['navigate-to-string', 'execute-script', 'post-message'].forEach(function (name) {
                var li = document.createElement('li');
                li.textContent = name;
                list.appendChild(li);
              });
              // WebView2 的 API 形状：window.chrome.webview.postMessage
              window.chrome.webview.postMessage({ hello: 'world', from: 'self-test', at: Date.now() });
              window.chrome.webview.postMessage('plain-string-payload');
            });
          </script>
        </body>
        </html>
        """;

    private static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout, string what)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
        {
            throw new TimeoutException($"{what} 超时（{timeout.TotalSeconds:0}s）");
        }

        return await task;
    }

    private static void Check(bool condition, string description)
    {
        Console.WriteLine($"[{(condition ? "PASS" : "FAIL")}] {description}");
        Evidence.AppendLine($"[{(condition ? "PASS" : "FAIL")}] {description}");
        if (!condition)
        {
            Failures.Add(description);
        }
    }

    private static void Fail(string message)
    {
        Check(false, message);
    }

    private static void PrintProbe()
    {
        ProbeResult probe = WebViewAvailability.ProbeResult;
        Console.WriteLine($"available       = {probe.IsAvailable}");
        Console.WriteLine($"webkit2gtk-4.1  = {probe.WebKitVersion ?? "<missing>"}");
        Console.WriteLine($"gtk+-3.0        = {probe.GtkVersion ?? "<missing>"}");
        Console.WriteLine($"missing          = [{string.Join(", ", probe.MissingLibraries)}]");
        Console.WriteLine($"browserVersion   = {(probe.IsAvailable ? probe.GetBrowserVersionString() : "<n/a>")}");
        if (probe.MissingLibraries.Count > 0)
        {
            Console.WriteLine(probe.InstallHint);
        }
    }

    private static void WriteLog(string outDir)
    {
        try
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "selftest.log"), Evidence.ToString());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"写日志失败：{ex.Message}");
        }
    }

    private static string? ReadArgument(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static bool TryReadXid(string[] args, out ulong xid)
    {
        xid = 0;
        string? raw = ReadArgument(args, "--embed");
        if (raw is null)
        {
            return false;
        }

        raw = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? raw[2..] : raw;
        return ulong.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out xid);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LunarWard.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
