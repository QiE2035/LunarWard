using Avalonia.Interactivity;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Messaging;
using LunarWard.Core;
using LunarWard.Frameworks;
using System;
using System.Text.Json.Nodes;
using System.Web;


namespace LunarWard.Features.Gacha;

/// <summary>
/// 上游 <c>Starward.Features.Gacha.CloudGameGachaWindow</c> 的移植。
/// </summary>
/// <remarks>
/// WebView2 → <see cref="Frameworks.WebView.WebView"/>（WebKitGTK，保留同一套
/// <c>EnsureCoreWebView2Async()</c> / <c>CoreWebView2.ExecuteScriptAsync(...)</c> API）；
/// WinUI 的 <c>Visibility</c> → <see cref="Avalonia.Controls.IsVisibleProperty"/>（docs/09）。
/// </remarks>
public sealed partial class CloudGameGachaWindow : WindowEx
{


    private const string SPAN_WEB_PREFIX_YS_CN = "https://webstatic.mihoyo.com/hk4e/event/e20190909gacha";

    private const string SPAN_WEB_PREFIX_SR_CN = "https://webstatic.mihoyo.com/hkrpg/event/e20211215gacha";



    public GameBiz GameBiz { get; set; }




    public CloudGameGachaWindow()
    {
        this.InitializeComponent();
        // 上游：AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;（Avalonia 由窗口管理器绘制）
        if (ShouldAppsUseDarkMode())
        {
            RootGrid.RequestedThemeVariant = ThemeVariant.Dark;
        }
        else
        {
            RootGrid.RequestedThemeVariant = ThemeVariant.Light;
        }
        AdaptTitleBarButtonColorToActuallTheme();
    }



    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // todo WinAppSDK 升级到 1.5 后，为云游戏的网页缓存设置单独的文件夹
            if (GameBiz.ToGame() == GameBiz.hk4e)
            {
                webview.Source = new Uri("https://ys.mihoyo.com/cloud/");
            }
            if (GameBiz.ToGame() == GameBiz.hkrpg)
            {
                webview.Source = new Uri("https://sr.mihoyo.com/cloud/");
            }
        }
        catch (Exception)
        {

        }
    }





    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await webview.EnsureCoreWebView2Async();
            string result = await webview.CoreWebView2!.ExecuteScriptAsync("document.documentElement.outerHTML");
            string? html = JsonNode.Parse(result)?.ToString();
            if (!string.IsNullOrWhiteSpace(html))
            {
                string? url = GetMatchUrl(GameBiz, html);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    url = HttpUtility.HtmlDecode(url);
                    WeakReferenceMessenger.Default.Send(new UpdateGachaLogMessage(GameBiz, url));
                    TextBlock_Info.IsVisible = true;
                    TextBlock_Error.IsVisible = false;
                    return;
                }
            }
            TextBlock_Info.IsVisible = false;
            TextBlock_Error.IsVisible = true;
            TextBlock_Error.Text = Lang.GachaLogPage_CannotFindURL;
        }
        catch (Exception ex)
        {
            TextBlock_Info.IsVisible = false;
            TextBlock_Error.IsVisible = true;
            TextBlock_Error.Text = ex.Message;
        }
    }




    private static string? GetMatchUrl(GameBiz gameBiz, string html)
    {
        string? prefix = gameBiz.ToGame().Value switch
        {
            GameBiz.hk4e => SPAN_WEB_PREFIX_YS_CN,
            GameBiz.hkrpg => SPAN_WEB_PREFIX_SR_CN,
            _ => null
        };
        if (prefix is not null)
        {
            ReadOnlySpan<char> span = html.AsSpan();
            int index = span.LastIndexOf(prefix);
            if (index >= 0)
            {
                var length = span[index..].IndexOfAny("\"");
                return new(span.Slice(index, length));
            }
        }
        return null;
    }




}
