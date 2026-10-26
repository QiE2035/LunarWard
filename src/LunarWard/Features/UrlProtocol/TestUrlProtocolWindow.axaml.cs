using Avalonia.Media.Imaging;
using Avalonia.Styling;
using LunarWard.Frameworks;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace LunarWard.Features.UrlProtocol;

/// <summary>
/// 上游 <c>Starward.Features.UrlProtocol.TestUrlProtocolWindow</c> 的移植。
/// </summary>
/// <remarks>
/// 平台差异（详见 docs/09-LINUX-DIVERGENCES.md）：
/// Mica/DesktopAcrylic 背景、<c>AppWindow.TitleBar.ExtendsContentIntoTitleBar</c>、
/// <c>OverlappedPresenter</c> 的窗口按钮控制，在 Avalonia 下分别退化为默认窗口、
/// <c>ExtendClientAreaToDecorationsHint</c> 与 <c>CanMaximize/CanResize</c> 属性。
/// </remarks>
public sealed partial class TestUrlProtocolWindow : WindowEx
{

    public TestUrlProtocolWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
    }


    private void InitializeWindow()
    {
        // 上游 AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        // Avalonia 的等价物是 ExtendClientAreaToDecorationsHint（WindowEx 里已是默认关闭，
        // 这里不做视觉改动，保持与上游一致的居中固定尺寸窗口）。
        if (ExtendClientAreaToDecorationsHint)
        {
            ExtendClientAreaToDecorationsHint = true;
        }
        if (!ShouldAppsUseDarkMode())
        {
            RootGrid.RequestedThemeVariant = ThemeVariant.Light;
        }
        // 上游 MicaController/DesktopAcrylicController 支持时切换系统背景；Avalonia 无等价 API，
        // 窗口保持默认背景（docs/09）。
        CenterInScreen(480, 400);
        AdaptTitleBarButtonColorToActuallTheme();
        CanMaximize = false;
        CanResize = false;
        TextBlock_ExePath.Text = Process.GetCurrentProcess().MainModule?.FileName;
        TextBlock_Argu.Text = string.Join(' ', Environment.GetCommandLineArgs().Skip(1));

        // 上游 ms-appx:///Assets/logo.png；本仓库的品牌图由 Starward.Assets 内容包拷到输出 Assets/。
        try
        {
            string logo = Path.Join(AppContext.BaseDirectory, "Assets", "logo.png");
            if (File.Exists(logo))
            {
                LogoImage.Source = new Bitmap(logo);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


}
