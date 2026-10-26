using LunarWard.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LunarWard.ViewModels;
using LunarWard.ViewModels.Starward;
using LunarWard.Views;
using LunarWard.Views.Starward;
using LunarWard.Platform;

namespace LunarWard;

public partial class App : Application
{
    private Services.LogService? _log;

    private ShellViewModel? _shell;

    private Services.TrayService? _tray;

    /// <summary>关闭窗口时是否最小化到托盘（由 <see cref="Services.TrayService"/> 是否建成功决定）。</summary>
    internal bool HasTray => _tray is not null;


    /// <summary>
    /// 正在真正退出进程（托盘菜单「退出」/ <c>--quit-after</c>）。此时主窗口 <c>OnClosing</c>
    /// 必须直接放行，不能再按关闭选项弹询问或隐藏到托盘。
    /// </summary>
    internal bool IsExiting { get; private set; }


    /// <summary>
    /// 按需创建托盘（用户在设置里刚把关闭行为改成「隐藏到托盘」时，重启前也能用）。
    /// </summary>
    internal bool EnsureTray()
    {
        if (_tray is not null)
        {
            return true;
        }

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window } desktop)
        {
            return false;
        }

        _tray = Services.TrayService.TryCreate(window, () => ExitFromTray(desktop));
        return _tray is not null;
    }


    /// <summary>托盘菜单「退出」：先标记真正退出，再关窗，避免被「每次询问 / 隐藏到托盘」拦住。</summary>
    private void ExitFromTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        IsExiting = true;
        desktop.Shutdown();
    }


    public override void Initialize() => AvaloniaXamlLoader.Load(this);


    public override void OnFrameworkInitializationCompleted()
    {
        var paths = XdgPaths.Default;
        AppServices.Initialize(paths);
        _log = new Services.LogService(paths);
        _log.Info($"LunarWard 启动，配置目录 {paths.ConfigHome}");

        if (AppServices.Current.ConfigWarning is { } warning)
        {
            _log.Warn(warning);
        }

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        if (CaptureMode.Language is { Length: > 0 } language)
        {
            Localization.AppStrings.Instance.Language = Localization.AppStrings.Normalize(language);
        }

        _shell = new ShellViewModel(AppServices.Current, _log);
        if (CaptureMode.ProfileId is { } profileId)
        {
            _shell.SelectProfileById(profileId);
        }

        var window = new MainWindow { DataContext = _shell };
        desktop.MainWindow = window;

        // 自检：--tray-test 无论配置如何都试建一次托盘，用输出判断当前桌面是否支持。
        if (CaptureMode.TrayTest)
        {
            AppServices.Current.Config.Launcher.CloseWindowOption = "Hide";
            _tray = Services.TrayService.TryCreate(window, () => ExitFromTray(desktop));
            Console.WriteLine(_tray is null ? "TRAY_FAIL" : "TRAY_OK");

            // 再验证「关闭 = 隐藏」：2 秒后调 Close，窗口应仍存活且不可见，然后主动退出。
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    window.Close();
                    Console.WriteLine($"WINDOW_VISIBLE_AFTER_CLOSE {window.IsVisible}");
                    desktop.Shutdown();
                });
            });
        }

        if (CaptureMode.QuitAfterMilliseconds > 0)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(CaptureMode.QuitAfterMilliseconds);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ExitFromTray(desktop));
            });
        }

        // 单实例监听：后来的启动请求都会走到这里，把窗口显示出来。
        if (!CaptureMode.Enabled)
        {
            Services.SingleInstance.Listen(() =>
            {
                Console.WriteLine("SINGLE_INSTANCE_ACTIVATED");
                window.Show();
                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }

                window.Activate();
            });
        }

        // 系统托盘：用户选了「最小化到系统托盘」，或本次是开机自启（--start-hidden）时创建；
        // 截图自检不碰托盘。开机自启必须建托盘，否则藏起来的窗口没有入口（只剩单实例救场）。
        bool closeToTray = string.Equals(
            AppServices.Current.Config.Launcher.CloseWindowOption, "Hide", StringComparison.OrdinalIgnoreCase);
        if (!CaptureMode.Enabled && (closeToTray || CaptureMode.StartHidden))
        {
            _tray = Services.TrayService.TryCreate(window, () => ExitFromTray(desktop));
            if (_tray is null)
            {
                // 桌面没有 SNI 宿主：把配置改回「完全退出」，设置页显示的就是实际行为。
                // 开机自启场景不改配置（用户没做这个选择），只是 --start-hidden 时会退回普通窗口。
                if (closeToTray)
                {
                    AppServices.Current.Config.Launcher.CloseWindowOption = "Exit";
                    AppServices.Current.Config.Save(AppServices.Current.Paths.ConfigFile);
                }

                if (CaptureMode.StartHidden)
                {
                    window.Show();
                }
            }
        }

        // 自检：--page <name> 直接落到指定页面
        if (CaptureMode.Page is { Length: > 0 } page && TryParsePage(page, out var shellPage))
        {
            _shell.Navigate(shellPage);
        }

        desktop.Exit += (_, _) =>
        {
            // 把 WAL 合并回主库，避免明文凭据留在 0644 的 -wal 里（见 LunarWardDatabase.Checkpoint）。
            LunarWard.Launch.Database.LunarWardDatabase.CheckpointDefault(AppServices.Current.Paths.DataHome);
            // 安装任务在后台线程继续下载/解压，不显式停掉会让进程退不干净（审计 M-05）。
            AppServices.Current.ShutdownInstaller();
            Services.SingleInstance.Dispose();
            _tray?.Dispose();
            _tray = null;
            _shell.Shutdown();
            _shell.Dispose();
            AppServices.Current.Background.Dispose();
        };

        if (CaptureMode.StartHidden)
        {
            window.Hide();
        }

        // 旧窗口自检（--window settings 等）保留到对应页面验收通过
        if (CaptureMode.Enabled && CaptureMode.TargetWindow is not "main")
        {
            ShowLegacyWindow(desktop, CaptureMode.TargetWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }


    private void ShowLegacyWindow(IClassicDesktopStyleApplicationLifetime desktop, string target)
    {
        if (_shell is null)
        {
            return;
        }

        var profile = _shell.Profile;
        Window? window = target switch
        {
            // 只保留仍是独立窗口的两个：账号/便笺已并入工具箱页
            "settings" => new SettingsWindow { DataContext = new SettingsViewModel(AppServices.Current, profile) },
            "account" => new AccountWindow { DataContext = new AccountViewModel(AppServices.Current, profile) },
            "dailynote" => new DailyNoteWindow { DataContext = new DailyNoteViewModel(AppServices.Current, profile) },
            _ => null,
        };

        if (window is null)
        {
            return;
        }

        window.Opened += (_, _) => CaptureMode.Capture(window);
        window.Show();
    }


    private static bool TryParsePage(string text, out ShellPage page)
    {
        switch (text.ToLowerInvariant())
        {
            case "launcher":
                page = ShellPage.Launcher;
                return true;
            case "game-setting":
            case "gamesetting":
                page = ShellPage.GameSetting;
                return true;
            case "screenshot":
            case "screenshots":
                page = ShellPage.Screenshot;
                return true;
            case "gacha":
                page = ShellPage.Gacha;
                return true;
            case "toolbox":
                page = ShellPage.Toolbox;
                return true;
            case "self-query":
            case "selfquery":
            case "account-records":
                page = ShellPage.SelfQuery;
                return true;
            case "genshin-beyond":
            case "beyond":
            case "miliastra":
                page = ShellPage.GenshinBeyondGacha;
                return true;
            case "settings":
            case "setting":
                page = ShellPage.Settings;
                return true;
            default:
                page = ShellPage.Launcher;
                return false;
        }
    }
}
