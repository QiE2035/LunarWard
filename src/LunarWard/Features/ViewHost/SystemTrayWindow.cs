using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LunarWard.Frameworks;
using Serilog;
using System;
using System.IO;

namespace LunarWard.Features.ViewHost;

/// <remarks>
/// 上游 <c>src/Starward/Features/ViewHost/SystemTrayWindow.xaml(.cs)</c> 的移植。
/// <para>
/// 上游做法：一个无边框的 <c>WindowEx</c> 承载「启动器 / 退出」两个按钮，右击托盘图标时
/// 用 <c>CalculatePopupWindowPosition</c> 定位弹出；图标本身由 <c>H.NotifyIcon.TaskbarIcon</c> 提供。
/// </para>
/// <para>
/// Linux 等价实现：托盘图标用 Avalonia 内建的 <see cref="TrayIcon"/>（X11/Wayland 上走
/// StatusNotifierItem/AppIndicator），菜单用 <see cref="NativeMenu"/>（由桌面环境原生绘制，
/// 可正确适配主题、缩放与工作区边缘）。因此不再需要自绘弹出窗口，本类退化为托盘宿主：
/// 左键点击显示/激活主窗口，右键弹出原生菜单。上游 .xaml 的按钮布局由原生菜单项等价表达，
/// 见 docs/09-LINUX-DIVERGENCES.md「五、ViewHost」与 docs/11-PORT-BACKLOG.md 白名单。
/// </para>
/// </remarks>
public sealed class SystemTrayWindow : WindowEx, IDisposable
{

    private readonly TrayIcon _trayIcon;

    private readonly Window? _mainWindow;

    private bool _disposed;


    public SystemTrayWindow(Window? mainWindow)
    {
        _mainWindow = mainWindow;

        // 与上游 InitializeWindow 一致：托盘宿主窗口本身永不显示
        ShowInTaskbar = false;
        CanResize = false;
        WindowState = WindowState.Minimized;
        Opened += (_, _) => Hide();

        _trayIcon = new TrayIcon
        {
            ToolTipText = "LunarWard",
            Icon = LoadIcon(),
            Menu = BuildMenu(),
            IsVisible = true,
        };
        // 左键：显示主窗口（上游 ShowMainWindowCommand）
        _trayIcon.Clicked += SystemTrayIcon_Clicked;
    }


    /// <summary>上游 <c>SetTrayIcon</c>：从进程图标资源加载；这里用随包分发的 .ico。</summary>
    private static WindowIcon? LoadIcon()
    {
        try
        {
            var uri = new Uri("avares://LunarWard/Assets/lunarward.ico");
            using Stream stream = AssetLoader.Open(uri);
            return new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Load tray icon failed");
            return null;
        }
    }


    /// <summary>上游 XAML 的「启动器 / 退出」两项菜单。</summary>
    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();

        var launcher = new NativeMenuItem(Lang.MainPage_Launcer);
        launcher.Click += (_, _) => ShowMainWindow();
        menu.Add(launcher);

        var exit = new NativeMenuItem(Lang.Common_Exit);
        exit.Click += (_, _) => ExitApplication();
        menu.Add(exit);

        return menu;
    }


    private void SystemTrayIcon_Clicked(object? sender, EventArgs e) => ShowMainWindow();


    /// <summary>上游 <c>ShowMainWindowCommand</c> → <c>App.Current.EnsureMainWindow()</c>。</summary>
    public void ShowMainWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_mainWindow is null)
            {
                return;
            }
            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            _mainWindow.Activate();
        });
    }


    /// <summary>上游 <c>ExitCommand</c> → <c>App.Current.Exit()</c>。</summary>
    public void ExitApplication()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        });
    }


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        try
        {
            _trayIcon.Clicked -= SystemTrayIcon_Clicked;
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Dispose tray icon");
        }
    }

}
