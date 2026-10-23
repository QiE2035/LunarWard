using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

        // 自检：--open-selector 直接把游戏选择层打开
        if (CaptureMode.OpenSelector && DataContext is ShellViewModel shell)
        {
            shell.OpenGameSelectorCommand.Execute(null);
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


    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;


    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
