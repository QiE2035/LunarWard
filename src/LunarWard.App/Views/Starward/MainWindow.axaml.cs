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
                Shell?.SelectToolboxTab(tab.Equals("accounts", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
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

        await Task.Delay(CaptureMode.WaitMilliseconds);
        CaptureMode.Capture(this);
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
