using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LunarWard.App.ViewModels;
using LunarWard.App.ViewModels.Starward;
using LunarWard.App.Views;
using LunarWard.App.Views.Starward;
using LunarWard.Platform;

namespace LunarWard.App;

public partial class App : Application
{
    private Services.LogService? _log;

    private ShellViewModel? _shell;


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

        // 自检：--page <name> 直接落到指定页面
        if (CaptureMode.Page is { Length: > 0 } page && TryParsePage(page, out var shellPage))
        {
            _shell.Navigate(shellPage);
        }

        desktop.Exit += (_, _) =>
        {
            _shell.Shutdown();
            _shell.Dispose();
            AppServices.Current.Background.Dispose();
        };

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
