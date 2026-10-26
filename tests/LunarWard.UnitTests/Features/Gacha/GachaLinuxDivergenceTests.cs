using System.Reflection;
using Xunit;

namespace LunarWard.UnitTests.Features.Gacha;

/// <summary>
/// <c>docs/13-GACHA-AUDIT.md</c> §五「未覆盖风险 G-1..G-8」的结构化断言：
/// 能在无数据库、无 UI 线程、无 WebKitGTK 的前提下定住的形态都定住，
/// 真正只能靠人眼或集成测试的（G-2 / G-3）在这里写明为何断言不了。
/// </summary>
public class GachaLinuxDivergenceTests
{

    private static Assembly AppAssembly => typeof(LunarWard.Features.Gacha.GachaLogItemEx).Assembly;


    /// <summary>
    /// G-1：本地方案拿不到 WebView 响应事件的**请求头**，所以 <c>ZZZGachaInfoWindow</c> 里对
    /// <c>x-rpc-lang</c> 的改写无效（Cookie 里的 <c>mi18nLang</c> 仍可改写）。
    /// 断言：事件参数只暴露 Uri / Response，没有任何请求头访问器。
    /// </summary>
    [Fact]
    public void G1_WebResourceResponseArgs_ExposeNoRequestHeaders()
    {
        Type? args = AppAssembly.GetType("LunarWard.Frameworks.WebView.CoreWebView2WebResourceResponseReceivedEventArgs");

        Assert.NotNull(args);

        string[] members = args.GetProperties().Select(x => x.Name)
            .Concat(args.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name))
            .ToArray();

        Assert.Contains("Uri", members);
        Assert.Contains("Response", members);
        Assert.DoesNotContain(members, x => x.Contains("Request", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, x => x.Contains("Header", StringComparison.OrdinalIgnoreCase));
    }


    /// <summary>
    /// G-2：上游 <c>Launcher.LaunchFolderAsync(FolderLauncherOptions{ItemsToSelect})</c>（打开文件夹并选中文件）
    /// 换成 <c>xdg-open</c> 打开所在目录。**不可断言**：会真的拉起外部进程；且文件管理器是否「选中」无法在单测观察。
    /// </summary>
    [Fact]
    public void G2_RevealInFileManager_IsProcessBased_NotAssertable()
    {
        // 这里只锁定替代实现的位置（页面私有方法），不执行它。
        var page = AppAssembly.GetType("LunarWard.Features.Gacha.GachaLogPage");

        Assert.NotNull(page);
        Assert.Contains(
            page.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static),
            x => x.Name.Contains("RevealInFileManager", StringComparison.Ordinal));
    }


    /// <summary>
    /// G-3：WinUI 的 <c>AppWindow.TitleBar.ExtendsContentIntoTitleBar</c> / <c>DesktopAcrylicBackdrop</c>
    /// 在 Avalonia 由窗口管理器绘制，无等价 API。**不可断言**：真实标题栏外观只能靠截图比对。
    /// 只锁定本地沿用上游命名的主题适配入口存在。
    /// </summary>
    [Fact]
    public void G3_TitleBarThemeHook_KeepsUpstreamName()
    {
        var windowEx = AppAssembly.GetType("LunarWard.Frameworks.WindowEx");

        Assert.NotNull(windowEx);
        Assert.NotNull(windowEx.GetMethod("AdaptTitleBarButtonColorToActuallTheme", BindingFlags.Public | BindingFlags.Instance));
    }


    /// <summary>
    /// G-4：上游用 <c>InfoBar</c> + <c>InAppToast.MainWindow.Show(infoBar)</c> 出进度条；
    /// 本地用页面内的 <c>GachaProgressToast</c>（可更新消息 + 取消按钮）。
    /// 断言该等价物存在、可更新标题/消息、有 <c>Show</c>/<c>Close</c> 两个入口。
    /// </summary>
    [Fact]
    public void G4_GachaProgressToast_ProvidesUpstreamProgressSurface()
    {
        Type? toast = AppAssembly.GetType("LunarWard.Features.Gacha.GachaProgressToast");

        Assert.NotNull(toast);
        Assert.True(typeof(Avalonia.Controls.Border).IsAssignableFrom(toast));
        Assert.NotNull(toast.GetMethod("Show", BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(toast.GetMethod("Close", BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(toast.GetProperty("Title"));
        Assert.NotNull(toast.GetProperty("Message"));
        Assert.NotNull(toast.GetMethod("HideCancelButton"));
        Assert.NotNull(toast.GetEvent("CancelClicked"));
    }


    /// <summary>
    /// G-5：Avalonia 没有 <c>TextBlock.IsTextTrimmedChanged</c>，统计卡的
    /// <c>TextBlock_GachaTypeText_IsTextTrimmedChanged</c> 已删除；<c>ResetGachaTypeTextFontSize</c> 保留。
    /// </summary>
    [Theory]
    [InlineData("GachaStatsCard")]
    [InlineData("ZZZGachaStatsCard")]
    public void G5_StatsCard_DropsIsTextTrimmedChanged_ButKeepsFontSizeReset(string card)
    {
        Type? type = AppAssembly.GetType($"LunarWard.Features.Gacha.{card}");

        Assert.NotNull(type);
        Assert.Null(type.GetMethod("TextBlock_GachaTypeText_IsTextTrimmedChanged", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.NotNull(type.GetMethod("ResetGachaTypeTextFontSize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.NotNull(type.GetProperty("WarpTypeStats"));
    }


    /// <summary>
    /// G-6：Core 里遗留的 <c>SelfQueryService</c>（旧 MVVM 层还在用）与新移植的
    /// <c>Features.SelfQuery.SelfQueryService</c> 同名并存，靠 <c>AppConfig.ServiceProvider</c> 里的
    /// using 别名消歧；两份都需要存在（Core 那份由 lead 登记为 endgame 删除项）。
    /// </summary>
    [Fact]
    public void G6_BothSelfQueryServices_ExistAndAreDistinct()
    {
        Type? core = typeof(LunarWard.Core.GameBiz).Assembly.GetType("LunarWard.Core.SelfQuery.SelfQueryService");
        Type? feature = AppAssembly.GetType("LunarWard.Features.SelfQuery.SelfQueryService");

        Assert.NotNull(core);
        Assert.NotNull(feature);
        Assert.NotEqual(core, feature);
        Assert.Equal("LunarWard.Features.SelfQuery", feature.Namespace);
    }


    /// <summary>
    /// G-7：叠加层窗口未移植，<c>RunningGameService.OpenOverlayWindow()</c> 保持上游「恒 false」的行为；
    /// 云游戏簇只用到 <c>AddRuninngGame</c>，不依赖叠加层。
    /// </summary>
    [Fact]
    public void G7_OpenOverlayWindow_StaysFalse()
    {
        Type? service = AppAssembly.GetType("LunarWard.Features.Overlay.RunningGameService");

        Assert.NotNull(service);

        MethodInfo? open = service.GetMethod("OpenOverlayWindow", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(open);
        Assert.Equal(typeof(bool), open.ReturnType);
        Assert.False((bool)open.Invoke(null, null)!);
    }


    /// <summary>
    /// G-8：上游连接串固定 <c>Cache=Shared;Mode=ReadWriteCreate;Pooling=True</c>，本地由
    /// <c>Features.Database.DatabaseService.CreateConnection()</c> 拼装（库文件位置不同，见 docs/13）。
    /// 断言：该入口仍是静态无参方法。
    /// </summary>
    [Fact]
    public void G8_DatabaseService_KeepsStaticCreateConnection()
    {
        Type? type = AppAssembly.GetType("LunarWard.Features.Database.DatabaseService");

        Assert.NotNull(type);

        MethodInfo? create = type.GetMethod("CreateConnection", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
        Assert.NotNull(create);
        Assert.Equal("Microsoft.Data.Sqlite.SqliteConnection", create.ReturnType.FullName);
    }

}
