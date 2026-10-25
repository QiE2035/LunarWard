using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward;

/// <summary>
/// Starward 主窗口（1200×676）。视图层只处理：标题栏拖拽与三个标题栏按钮。
/// 其余一切在 <see cref="ShellViewModel"/> 与各页面里。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }


    private void OnOpened(object? sender, EventArgs e)
    {
        if (!CaptureMode.Enabled)
        {
            return;
        }

        if (DataContext is ShellViewModel shell)
        {
            if (CaptureMode.ExpandRail)
            {
                shell.IsRailExpanded = true;
            }

            if (CaptureMode.OpenSelector)
            {
                shell.OpenGameSelectorCommand.Execute(null);
                if (CaptureMode.SelectorHover)
                {
                    _ = SetSelectorHoverAsync(this);
                }

                if (CaptureMode.SelectorFlyout)
                {
                    _ = ShowSelectorFlyoutAsync(this);
                }
            }

            if (CaptureMode.GameIcons)
            {
                shell.ShowGameIcons();
            }

            _ = AwaitReadyThenCaptureAsync(shell);
            return;
        }

        CaptureMode.Capture(this);
    }


    /// <summary>自检：把游戏选择器第一张卡片切成 <c>:pointerover</c>，截「选择游戏服务器」遮罩。</summary>
    private static async Task SetSelectorHoverAsync(Control root)
    {
        await Task.Delay(600);
        // 直接点亮第一张卡片的悬停遮罩（真实交互仍靠 Border.choice:pointerover 样式）
        var overlay = root.GetVisualDescendants()
                          .OfType<Avalonia.Controls.Border>()
                          .FirstOrDefault(x => x.Classes.Contains("choiceHover"));
        if (overlay is not null)
        {
            overlay.Opacity = 1;
        }
    }


    /// <summary>自检：弹出第一张卡片的区服列表，并打印实际是否打开（Flyout 是独立 Popup，不随窗口截图）。</summary>
    private static async Task ShowSelectorFlyoutAsync(Control root)
    {
        await Task.Delay(600);
        var card = root.GetVisualDescendants()
                       .OfType<Avalonia.Controls.Border>()
                       .FirstOrDefault(x => x.Classes.Contains("choice"));
        if (card?.ContextFlyout is { } flyout)
        {
            flyout.ShowAt(card);
            await Task.Delay(400);
            Console.WriteLine($"SERVER_FLYOUT_OPEN {flyout.IsOpen}");
        }
        else
        {
            Console.WriteLine("SERVER_FLYOUT_OPEN False (card/flyout not found)");
        }
    }


    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.ClickCount == 2)
        {
            // Starward 主窗口不可最大化：双击标题栏不动作
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }


    /// <summary>
    /// 自检截图：先等外壳的素材加载完成（背景/公告/图标都可能走网络），
    /// 之后再多等 <see cref="CaptureMode.WaitMilliseconds"/> 让绑定与图片解码落地。
    /// </summary>
    private async Task AwaitReadyThenCaptureAsync(ShellViewModel shell)
    {
        try
        {
            await Task.WhenAny(shell.InitializationTask, Task.Delay(15000));
        }
        catch (Exception)
        {
            // 素材失败不影响截图
        }

        // 工具箱 Tab：等页面挂好后再切
        if (CaptureMode.Tab is { Length: > 0 } tab)
        {
            try
            {
                await Task.Delay(500);
                var index = int.TryParse(tab, out var parsed)
                    ? parsed
                    : tab.Equals("accounts", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                Shell?.SelectToolboxTab(index);
            }
            catch (Exception)
            {
                // 自检辅助
            }
        }

        // 设置子页：--page settings --settings-section <tag>
        // 设置页在 Frame 里，等视觉树挂好后按 tag 切二级导航（自检每页出图用）。
        if (CaptureMode.SettingsSection is { Length: > 0 } settingsSection)
        {
            await SelectSettingsSectionAsync(settingsSection);
        }

        if (CaptureMode.ScrollY > 0)
        {
            await Task.Delay(300);
            ScrollPage(CaptureMode.ScrollY);
            await Task.Delay(300);
        }

        // 时长统计弹窗：自检时单独截这个窗口（它是独立 Window，主窗口渲染不到）
        if (string.Equals(CaptureMode.TargetWindow, "playtime", StringComparison.OrdinalIgnoreCase))
        {
            var dialog = new PlayTimeStatsDialog(shell.Services, GameLauncherPageViewModel.GameBizOfProfile(shell.Profile));
            dialog.Show(this);
            dialog.CaptureSelf(CaptureMode.Path!, 1200);
            return;
        }

        // 图片查看器：同样是独立 Window，主窗口渲染不到
        if (string.Equals(CaptureMode.TargetWindow, "image-viewer", StringComparison.OrdinalIgnoreCase))
        {
            var viewer = new ImageViewWindow2(ImageViewWindow2.CreateCaptureSamples(), 0);
            viewer.PrepareCapture(CaptureMode.ImageViewerGallery, CaptureMode.ImageViewerInfo);
            viewer.Show(this);
            viewer.CaptureSelf(CaptureMode.Path!, 1600);
            return;
        }

        await Task.Delay(CaptureMode.WaitMilliseconds);
        CaptureMode.Capture(this);
    }


    /// <summary>
    /// 自检截图辅助：把窗口里最大的 ScrollViewer 滚到指定位置（截长页面下半部分用）。
    /// </summary>
    private void ScrollPage(double y)
    {
        var viewer = this.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .Where(x => x.IsVisible && x.Bounds.Width > 400 && x.Bounds.Height > 200)
            .OrderByDescending(x => x.Bounds.Width * x.Bounds.Height)
            .FirstOrDefault();
        if (viewer is not null)
        {
            viewer.Offset = new Avalonia.Vector(0, y);
        }
    }


    /// <summary>
    /// 等设置页挂到视觉树后调用 <c>SettingPage.SelectSection(tag)</c>。
    /// </summary>
    /// <remarks>
    /// 设置页由 <c>StarwardShell</c> 的 <c>PageHost</c> 承载，<c>OnOpened</c> 时可能还没
    /// 完成挂载，所以最多重试 3 秒；找不到就交给正常截图（不抛错）。
    /// </remarks>
    private async Task SelectSettingsSectionAsync(string tag)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            var page = Shell?.GetVisualDescendants().OfType<Pages.SettingPage>().FirstOrDefault();
            if (page is not null)
            {
                page.SelectSection(tag);
                // 让新子页的排版与绑定跑完一帧再截图
                await Task.Delay(300);
                return;
            }

            await Task.Delay(200);
        }
    }


    private async Task ApplyToolboxTabAsync(string tab)
    {
        try
        {
            await Task.Delay(800);
            Shell?.SelectToolboxTab(tab.Equals("accounts", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
        }
        catch (Exception)
        {
            // 自检辅助，失败不影响截图
        }
    }


    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;


    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
