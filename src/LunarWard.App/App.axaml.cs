using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LunarWard.App.ViewModels;
using LunarWard.App.ViewModels;
using LunarWard.App.Views;
using LunarWard.Platform;

namespace LunarWard.App;

public partial class App : Application
{
    private Services.LogService? _log;


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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel(AppServices.Current, _log);

            // 自检：--screenshot <png> --window settings 直接开设置窗口
            if (CaptureMode.Enabled && CaptureMode.TargetWindow == "settings")
            {
                var settings = new SettingsWindow
                {
                    DataContext = new SettingsViewModel(AppServices.Current, viewModel.Profiles[0]),
                };
                var mainWindow = new MainWindow { DataContext = viewModel };
                desktop.MainWindow = mainWindow;
                settings.Opened += (_, _) => CaptureMode.Capture(settings);
                settings.Show();
                base.OnFrameworkInitializationCompleted();
                return;
            }

            // URL 协议：lunarward://launch?profile=… （由 .desktop 的 %U 传入）
            var command = Launch.UrlProtocol.TryParse(Environment.GetCommandLineArgs().Skip(1), out var protocolError);
            if (protocolError is not null)
            {
                _log.Warn($"URL 协议：{protocolError}");
            }

            if (command is not null)
            {
                _log.Info($"URL 协议：{Launch.UrlProtocol.Describe(command)}");
                viewModel.ApplyUrlCommand(command);
            }

            // 自检：--screenshot <png> --window dailynote
            if (CaptureMode.Enabled && CaptureMode.TargetWindow == "dailynote")
            {
                var mainWindowNote = new MainWindow { DataContext = viewModel };
                desktop.MainWindow = mainWindowNote;
                var note = new DailyNoteWindow
                {
                    DataContext = new DailyNoteViewModel(AppServices.Current, viewModel.Profiles[0]),
                };
                note.Opened += (_, _) => CaptureMode.Capture(note);
                note.Show();
                base.OnFrameworkInitializationCompleted();
                return;
            }

            // 自检：--screenshot <png> --window gacha
            if (CaptureMode.Enabled && CaptureMode.TargetWindow == "gacha")
            {
                var mainWindowGacha = new MainWindow { DataContext = viewModel };
                desktop.MainWindow = mainWindowGacha;
                var gacha = new GachaWindow
                {
                    DataContext = new GachaViewModel(AppServices.Current, viewModel.Profiles[0]),
                };
                gacha.Opened += (_, _) => CaptureMode.Capture(gacha);
                gacha.Show();
                base.OnFrameworkInitializationCompleted();
                return;
            }

            var window = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                viewModel.Shutdown();
                AppServices.Current.Background.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
