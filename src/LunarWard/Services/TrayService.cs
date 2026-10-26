using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace LunarWard.Services;

/// <summary>
/// 系统托盘图标（对应上游 <c>Features/ViewHost/SystemTrayWindow</c> 的托盘入口）。
/// </summary>
/// <remarks>
/// 上游用 <c>H.NotifyIcon</c> + 一扇无边框的托盘窗口来弹菜单；Avalonia 自带
/// <see cref="TrayIcon"/> + <see cref="NativeMenu"/>，语义等价，直接用原生菜单。
/// Linux 上走 StatusNotifier（KDE 默认有、GNOME 需要 AppIndicator 扩展）；宿主不存在时
/// 托盘可能不显示，所以关闭到托盘时仍保留窗口对象，`lunarward://open` / 再次启动可以带回。
/// </remarks>
public sealed class TrayService : IDisposable
{
    private readonly TrayIcon _tray;
    private readonly Window _window;
    private readonly Action _exit;
    private bool _disposed;


    public TrayService(Window window, Action exit)
    {
        _window = window;
        _exit = exit;

        var menu = new NativeMenu();
        var show = new NativeMenuItem("显示主窗口");
        show.Click += (_, _) => ShowWindow();
        var quit = new NativeMenuItem("退出");
        quit.Click += (_, _) => _exit();

        menu.Items.Add(show);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quit);

        _tray = new TrayIcon
        {
            Icon = LoadIcon(),
            ToolTipText = "LunarWard",
            Menu = menu,
            IsVisible = true,
        };
        _tray.Clicked += (_, _) => ShowWindow();

        // Avalonia 要求把 TrayIcon 挂到 Application 的 TrayIcons 集合里才会真正显示。
        if (Avalonia.Application.Current is { } app)
        {
            var icons = TrayIcon.GetIcons(app) ?? new TrayIcons();
            icons.Add(_tray);
            TrayIcon.SetIcons(app, icons);
        }
    }


    /// <summary>托盘是否创建成功（创建失败不抛错，返回 false）。</summary>
    public static TrayService? TryCreate(Window window, Action exit)
    {
        try
        {
            return new TrayService(window, exit);
        }
        catch (Exception ex)
        {
            // 没有 SNI / DBus / 图像解码失败：退回普通窗口，不影响主流程
            Console.Error.WriteLine($"TRAY_CREATE_FAILED {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }


    private static WindowIcon LoadIcon()
    {
        using Stream stream = AssetLoader.Open(new Uri("avares://LunarWard/Assets/lunarward.ico"));
        return new WindowIcon(stream);
    }


    private void ShowWindow()
    {
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tray.IsVisible = false;
        _tray.Dispose();
    }
}
