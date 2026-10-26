using LunarWard.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Text;
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


    private LunarWard.Features.ViewHost.SystemTrayWindow? _systemTrayWindow;


    /// <summary>
    /// 上游 <c>App.Current.EnsureSystemTray()</c> 的等价：确保原生托盘菜单（Avalonia TrayIcon）已创建。
    /// 旧 <see cref="Services.TrayService"/> 在 endgame 删除，这里先并存、互不干扰。
    /// </summary>
    public static void EnsureSystemTray()
    {
        if (Current is not App app)
        {
            return;
        }
        app._systemTrayWindow ??= new LunarWard.Features.ViewHost.SystemTrayWindow(
            (app.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
    }


    /// <summary>
    /// 上游 <c>App.OnLaunched</c> 的等价启动流程：环境检查（WelcomeWindow / NoPermissionWindow）→
    /// 建 <see cref="LunarWard.Features.ViewHost.MainWindow"/> → 单实例回收到它。
    /// </summary>
    private async void StartPortShell(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            LunarWard.Frameworks.StartupUi.NoPermissionHandler = async folder =>
            {
                var window = new LunarWard.Features.ViewHost.NoPermissionWindow(folder);
                await window.WaitAsync();
            };
            LunarWard.Frameworks.StartupUi.WelcomeHandler = async () =>
            {
                var window = new LunarWard.Features.ViewHost.WelcomeWindow();
                return await window.WaitAsync();
            };

            await AppConfig.CheckEnviromentAsync();

            var window = new LunarWard.Features.ViewHost.MainWindow();
            desktop.MainWindow = window;
            window.Show();

            Services.SingleInstance.Listen(EnsureMainWindow);
        }
        catch (Exception ex)
        {
            _log?.Error("启动失败", ex);
            Console.Error.WriteLine(ex);
            desktop.Shutdown(1);
        }
    }


    /// <summary>托盘菜单「退出」：先标记真正退出，再关窗，避免被「每次询问 / 隐藏到托盘」拦住。</summary>
    private void ExitFromTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        IsExiting = true;
        desktop.Shutdown();
    }


    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // 移植自上游 App.xaml.cs 构造函数：全局异常落盘（格式与上游一致）+ 60s 一次 GC。
        Dispatcher.UIThread.UnhandledException += App_UnhandledException;
        _gcTimer = new DispatcherTimer(TimeSpan.FromSeconds(60), DispatcherPriority.Background, (_, _) => GC.Collect());
        _gcTimer.Start();
    }


    private DispatcherTimer? _gcTimer;


    /// <summary>上游 <c>App.Current</c>。</summary>
    public static new App Current => (App)Application.Current!;


    private void App_UnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            string logFile = AppConfig.LogFile;
            if (string.IsNullOrWhiteSpace(logFile))
            {
                string logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunarWard", "log");
                Directory.CreateDirectory(logFolder);
                logFile = Path.Combine(logFolder, $"LunarWard_{DateTime.Now:yyMMdd}.log");
            }
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] App Crash:");
            sb.AppendLine(e.Exception.ToString());
            if (e.Exception.Data.Count > 0)
            {
                foreach (System.Collections.DictionaryEntry item in e.Exception.Data)
                {
                    sb.AppendLine($"{item.Key}: {item.Value}");
                }
            }
            using var fs = File.Open(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            using var sw = new StreamWriter(fs);
            sw.Write(sb);
        }
        catch
        {
            // 崩溃日志自身失败时不再抛出。
        }
    }


    /// <summary>上游 <c>App.EnsureMainWindow</c>：多实例转发激活时把主窗口显示出来。</summary>
    public void EnsureMainWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
            window.Show();
            window.Activate();
        }
    }


    /// <summary>上游 <c>App.Exit</c>：先恢复手柄 Guide 键，再关窗退出。</summary>
    /// <remarks>
    /// Avalonia 的 <see cref="Application"/> 没有同名无参成员，因此不需要 <c>new</c>（CS0109）。
    /// </remarks>
    public void Exit()
    {
        LunarWard.Features.GamepadControl.GamepadController.RestoreGamepadGuideButtonForGameBar();
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }


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

        // 正常启动走新外壳（上游 Features/ViewHost/MainWindow + MainView）；
        // --screenshot/--tray-test/--page 等自检路径暂时仍走旧壳，等自检迁移完成后删除。
        if (!CaptureMode.Enabled && Environment.GetEnvironmentVariable("LUNARWARD_LEGACY_SHELL") is not "1")
        {
            StartPortShell(desktop);
            base.OnFrameworkInitializationCompleted();
            return;
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
