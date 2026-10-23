using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
