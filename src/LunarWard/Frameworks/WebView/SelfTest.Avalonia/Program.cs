// Avalonia 侧嵌入自检：把 WebView 控件放进真实 Avalonia 窗口，验证
// NativeControlHost → X11 子窗口 → GtkPlug XEmbed → WebKitGTK 这条完整链路。
//
//   dotnet run --project src/LunarWard/Frameworks/WebView/SelfTest.Avalonia
//
// 退出码 0 = 全部通过；截图写到 artifacts/webview-selftest/avalonia-embed.png。

using System.Diagnostics;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

using LunarWard.Frameworks.WebView;

namespace LunarWard.Frameworks.WebView.SelfTestAvalonia;

internal static class Program
{
    private static readonly List<string> _results = new();
    private static WebView? _view;
    private static int _exitCode = 1;
    private static string _outputDirectory = string.Empty;
    private static StreamWriter? _evidence;

    [STAThread]
    public static int Main(string[] args)
    {
        _outputDirectory = ResolveOutputDirectory(args);
        Directory.CreateDirectory(_outputDirectory);
        _evidence = new StreamWriter(Path.Combine(_outputDirectory, "avalonia-selftest.log"), append: false, Encoding.UTF8)
        {
            AutoFlush = true,
        };

        WebViewLog.Sink += line => _evidence.WriteLine(line);

        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            Args = args,
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        AppBuilder.Configure<ProbeApp>()
            .UsePlatformDetect()
            .SetupWithLifetime(lifetime);

        var view = new WebView();
        var window = new Window
        {
            Title = "LunarWard WebView × Avalonia 嵌入自检",
            Width = 960,
            Height = 640,
            Content = view,
        };
        _view = view;
        window.Show();

        _ = RunChecksAsync(lifetime);

        int code = lifetime.Start(args);
        return code == 0 ? _exitCode : code;
    }

    private static async Task RunChecksAsync(ClassicDesktopStyleApplicationLifetime lifetime)
    {
        try
        {
            await Task.Delay(700).ConfigureAwait(true);

            CoreWebView2 core = await _view!.EnsureCoreWebView2Async().ConfigureAwait(true);
            Report("WebKitGTK 依赖可用", WebView.IsWebViewAvailable, WebView.MissingDependenciesMessage);
            Report("Avalonia NativeControlHost 走 XID 嵌入（非降级）", _view.IsEmbedded, _view.StatusMessage);

            var messageTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            core.WebMessageReceived += (_, e) => messageTcs.TrySetResult(e.WebMessageAsJson);

            core.NavigateToString("""
                <!doctype html><html><head><meta charset="utf-8"><title>AVALONIA-EMBED</title>
                <style>body{background:#101014;color:#eee;font-family:sans-serif;padding:24px}
                h1{color:#ffc53d;font-size:40px}li{margin:6px 0}</style></head>
                <body><h1>Avalonia 里的 WebKitGTK</h1>
                <p id="out">等待脚本…</p>
                <ul><li>NativeControlHost → X11 子窗口</li><li>gtk_plug_new 做 XEmbed</li>
                <li>JS ↔ C# 双向消息</li></ul>
                <script>
                document.getElementById('out').textContent = 'JS 已执行：' + (6 * 7);
                window.chrome.webview.postMessage({ host: 'avalonia', ok: true });
                </script></body></html>
                """);

            string json = await messageTcs.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(true);
            Report($"JS→C# 消息回传（{json}）", json.Contains("\"ok\":true", StringComparison.Ordinal), json);

            await Task.Delay(400).ConfigureAwait(true);

            string title = await core.ExecuteScriptAsync("document.title").ConfigureAwait(true);
            Report($"ExecuteScriptAsync(document.title) = {title}", title == "\"AVALONIA-EMBED\"", title);

            string text = await core.ExecuteScriptAsync("document.getElementById('out').textContent").ConfigureAwait(true);
            Report($"ExecuteScriptAsync 读到页面脚本写回的 DOM = {text}",
                text.Contains("42", StringComparison.Ordinal), text);

            // ---- 自定义 scheme 映射（SetVirtualHostNameToFolderMapping 的等价物）----
            string assets = Path.Combine(_outputDirectory, "mapped-assets");
            Directory.CreateDirectory(assets);
            await File.WriteAllTextAsync(
                Path.Combine(assets, "index.html"),
                "<!doctype html><html><head><meta charset=\"utf-8\"><title>APPMAPPED</title></head>"
                + "<body style=\"background:#101014;color:#eee;padding:24px\"><h1>虚拟主机映射</h1>"
                + "<p id=\"v\">appassets</p></body></html>").ConfigureAwait(true);

            core.SetVirtualHostNameToFolderMapping(
                "probe.local",
                assets,
                CoreWebView2HostResourceAccessKind.Allow);

            var mappedLoaded = new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void OnNavCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                if (e.Uri.Contains("probe.local", StringComparison.Ordinal))
                {
                    mappedLoaded.TrySetResult(e);
                }
            }

            core.NavigationCompleted += OnNavCompleted;
            core.Navigate("https://probe.local/index.html");
            CoreWebView2NavigationCompletedEventArgs mapped = await mappedLoaded.Task
                .WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(true);
            core.NavigationCompleted -= OnNavCompleted;

            Report($"https://probe.local/ 映射到本地目录并加载成功（uri={mapped.Uri}）",
                mapped.IsSuccess && mapped.Uri.Contains("probe.local", StringComparison.Ordinal),
                mapped.ErrorText);

            string mappedTitle = await core.ExecuteScriptAsync("document.title").ConfigureAwait(true);
            Report($"映射页标题 = {mappedTitle}", mappedTitle == "\"APPMAPPED\"", mappedTitle);

            // ---- ZoomFactor / DefaultBackgroundColor ----
            core.ZoomFactor = 1.5;
            core.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 16, 16, 20);
            string zoom = await core.ExecuteScriptAsync("String(window.devicePixelRatio)").ConfigureAwait(true);
            Report($"ZoomFactor=1.5 已生效（devicePixelRatio={zoom}）",
                double.TryParse(zoom.Trim('"'), out double ratio) && ratio > 1.0, zoom);

            using Stream pngStream = await core.CapturePreviewAsync().ConfigureAwait(true);
            using var buffer = new MemoryStream();
            await pngStream.CopyToAsync(buffer).ConfigureAwait(true);
            byte[] png = buffer.ToArray();
            string snapshotPath = Path.Combine(_outputDirectory, "avalonia-embed.png");
            await File.WriteAllBytesAsync(snapshotPath, png).ConfigureAwait(true);

            bool validPng = png.Length > 1000 && png[0] == 0x89 && png[1] == 0x50 && png[2] == 0x4E && png[3] == 0x47;
            Report($"截图 {snapshotPath}（{png.Length} 字节）", validPng, snapshotPath);

            _exitCode = _results.All(item => item.StartsWith("[PASS]", StringComparison.Ordinal)) ? 0 : 1;
        }
        catch (Exception ex)
        {
            _results.Add($"[FAIL] 自检异常：{ex}");
            Console.Error.WriteLine(ex);
            _exitCode = 1;
        }
        finally
        {
            Console.WriteLine();
            foreach (string line in _results)
            {
                Console.WriteLine(line);
            }

            Console.WriteLine(_exitCode == 0
                ? $"=== Avalonia 嵌入自检全部通过（{_outputDirectory}）==="
                : $"=== Avalonia 嵌入自检失败（{_outputDirectory}）===");
            _evidence?.Dispose();
            Dispatcher.UIThread.Post(() => lifetime.Shutdown(_exitCode));
        }
    }

    private static void Report(string name, bool ok, string? detail = null)
    {
        string line = (ok ? "[PASS] " : "[FAIL] ") + name;
        _results.Add(line);
        Console.WriteLine(line);
        if (!ok && !string.IsNullOrEmpty(detail))
        {
            Console.WriteLine($"       细节：{detail}");
        }
    }

    private static string ResolveOutputDirectory(string[] args)
    {
        int index = Array.IndexOf(args, "--out");
        if (index >= 0 && index + 1 < args.Length)
        {
            return Path.GetFullPath(args[index + 1]);
        }

        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "LunarWard.slnx")))
        {
            current = current.Parent;
        }

        string root = current?.FullName
                      ?? Directory.GetCurrentDirectory();

        return Path.Combine(root, "artifacts", "webview-selftest");
    }
}

internal sealed class ProbeApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}
