using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using LunarWard.Core;
using LunarWard.Frameworks;
using LunarWard.Frameworks.WebView;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Timers;

namespace LunarWard.Features.GameLauncher;

/// <summary>
/// 游戏内公告浮窗。<b>移植自</b>
/// <c>Starward/src/Starward/Features/GameLauncher/GameNoticeWindow.xaml(.cs)</c>。
/// </summary>
/// <remarks>
/// <para>
/// <b>平台替换</b>：WinUI <c>WebView2</c> → <see cref="Frameworks.WebView.WebView"/>（WebKitGTK，
/// 保留 WebView2 同形 API，注入的 JS/消息协议与上游完全一致）；
/// WinUI <c>AppWindow</c> 的定位/无边框装饰 → Avalonia 的 <c>CanResize/CanMaximize</c>，
/// 窗口位次由调用方给的尺寸决定（不再按父窗口 handle 贴边）。
/// </para>
/// </remarks>
public sealed partial class GameNoticeWindow : WindowEx
{


    private System.Timers.Timer? timer;


    private readonly ILogger<GameNoticeWindow> _logger = AppConfig.GetLogger<GameNoticeWindow>();


    private readonly GameNoticeService _gameNoticeService = AppConfig.GetService<GameNoticeService>();


    public GameBiz CurrentGameBiz { get; set; }


    public long CurrentUid { get; set; }


    public nint ParentWindowHandle { get; set; }



    public GameNoticeWindow()
    {
        this.InitializeComponent();
        // 平台替换：WinUI TransparentBackdrop → Avalonia 透明窗口
        this.TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        InitializeWindow();
        Closed += (_, _) => WeakReferenceMessenger.Default.Send(new GameNoticeWindowClosedMessage());
    }




    private void InitializeWindow()
    {
        try
        {
            Title = "Starward";
            CanMaximize = false;
            CanResize = false;
            ShowInTaskbar = false;
            ExtendClientAreaToDecorationsHint = true;
            this.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                }
            };
        }
        catch { }
    }





    public new void Activate()
    {
        // 平台替换：上游按父窗口 handle 取 AppWindow 的 Position/Size 贴边（留 8px 边）；
        // Avalonia 这里没有跨窗口 handle 映射，直接用调用方设定的 Width/Height 激活。
        base.Show();
    }




    private async void Grid_Loaded(object? sender, RoutedEventArgs e)
    {
        if (ParentWindowHandle is 0)
        {
            Close();
            return;
        }
        timer = new System.Timers.Timer(10000);
        timer.AutoReset = false;
        timer.Elapsed += (_, _) => Dispatcher.UIThread.Post(Close);
        timer.Start();
        await InitializeWebViewAsync();
    }



    private async Task InitializeWebViewAsync()
    {
        try
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            string url = _gameNoticeService.GetGameNoticeUrl(CurrentGameBiz);
            try
            {
                await webview.EnsureCoreWebView2Async();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Initialize WebView2 failed.");
                Close();
                return;
            }
            webview.CoreWebView2.ProcessFailed += (_, _) => Close();
            webview.CoreWebView2.NavigationCompleted += (_, e) => { if (!e.IsSuccess) Close(); };
            webview.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
            webview.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            webview.Source = new Uri(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize game notices");
            Close();
        }
    }



    private async void CoreWebView2_DOMContentLoaded(object? sender, object? args)
    {
        try
        {
            await webview.EnsureCoreWebView2Async();
            string script = $$"""
                document.onkeydown = (e) => {if(e.key === "Escape") chrome.webview.postMessage({ "action": "close" });}
                miHoYoGameJSSDK.closeWebview = () => chrome.webview.postMessage({ "action": "close" });
                miHoYoGameJSSDK.openInBrowser = (url) => chrome.webview.postMessage({ "action": "url", "param": url });
                miHoYoGameJSSDK.openInWebview = (url) => chrome.webview.postMessage({ "action": "url", "param": url });

                function RemoveBg() {
                    let root = document.getElementById("root");
                    if (root === null) {
                        window.setTimeout(RemoveBg, 100);
                    } else {
                        root.style.background = "transparent";
                        let home = document.getElementsByClassName("home");
                        let mask = document.getElementsByClassName("home__mask");
                        if (mask.length > 0) {
                            mask[0].remove();
                        } else if (home.length > 0) {
                            home[0].style.background = "transparent";
                        } else {
                            window.setTimeout(RemoveBg, 100);
                        }
                    }
                }

                function modifyAllFontFaces() {
                    for (let sheet of document.styleSheets) {
                        try {
                            for (let i = sheet.cssRules.length - 1; i >= 0; i--) {
                                const rule = sheet.cssRules[i];
                                if (rule instanceof CSSFontFaceRule) {
                                    if (rule.style.fontFamily.includes("bh3")) {
                                        let src = rule.style.getPropertyValue("src").replace("http://127.0.0.1:1221/font", "https://webstatic.mihoyo.com/bh3/upload/announcement/font");
                                        rule.style.setProperty("src", src);
                                    } else if (rule.style.fontFamily.includes("ys")) {
                                        let src = rule.style.getPropertyValue("src").replace("http://127.0.0.1:1221/font", "https://sdk.mihoyo.com/hk4e/fonts");
                                        rule.style.setProperty("src", src);
                                    } else if (rule.style.fontFamily.includes("rpg")) {
                                        let src = rule.style.getPropertyValue("src").replace("http://127.0.0.1:1221/font", "https://sdk.mihoyo.com/hkrpg/fonts");
                                        rule.style.setProperty("src", src);
                                    }
                                }
                            }
                        } catch (e) { }
                    }
                }

                RemoveBg();
                modifyAllFontFaces();
                """;
            await webview.CoreWebView2.ExecuteScriptAsync(script);
            webview.IsVisible = true;
            timer?.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Add js sdk");
            Close();
        }
    }



    private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var node = JsonNode.Parse(args.WebMessageAsJson);
            string? action = node?["action"]?.ToString();
            string? param = node?["param"]?.ToString();
            if (action is "close")
            {
                Close();
            }
            if (action is "url")
            {
                if (Uri.TryCreate(param, UriKind.RelativeOrAbsolute, out var uri))
                {
                    // 平台替换：WinUI Launcher.LaunchUriAsync → Avalonia TopLevel.Launcher
                    var launcher = TopLevel.GetTopLevel(this)?.Launcher;
                    if (launcher is not null)
                    {
                        await launcher.LaunchUriAsync(uri);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "web message received");
            Close();
        }
    }


}
