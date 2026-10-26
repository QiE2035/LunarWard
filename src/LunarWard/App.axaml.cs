using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.GameLauncher;
using LunarWard.Features.GamepadControl;
using LunarWard.Features.PlayTime;
using LunarWard.Features.UrlProtocol;
using LunarWard.Features.ViewHost;
using LunarWard.RPC;
using Microsoft.Extensions.Configuration;
using System.Collections;
using System.Text;

namespace LunarWard;

/// <summary>
/// 应用入口。结构对齐上游 <c>src/Starward/App.xaml.cs</c>：
/// 构造函数（本仓库是 <see cref="Initialize"/>）、<c>App_UnhandledException</c>、
/// <c>OnLaunched</c>（本仓库是 <see cref="OnFrameworkInitializationCompleted"/>）、
/// <c>HandleStartupAsync</c>、<c>EnsureMainWindow</c>、<c>EnsureSystemTray</c>、<c>Exit</c>。
/// </summary>
/// <remarks>
/// 平台替换点集中在本文件的三处：
/// <list type="bullet">
/// <item>上游 <c>Timer</c>（System.Timers）→ Avalonia <see cref="DispatcherTimer"/>；</item>
/// <item>上游 <c>AppInstance</c>（Windows 激活重定向）→ <c>Frameworks.SingleInstance</c>（Unix socket）；</item>
/// <item>上游 <c>OnLaunched</c> → Avalonia <see cref="OnFrameworkInitializationCompleted"/>。</item>
/// </list>
/// 自检（<c>--screenshot</c> / <c>--page</c>）是本仓库的开发工具，集中在本文件末尾的
/// <c>RunCaptureTooling</c>，不属于上游结构，调用点只有一处。
/// </remarks>
public partial class App : Application
{

    private DispatcherTimer? _gcTimer;

    public static new App Current => (App)Application.Current!;


    public override void Initialize()
    {
        // 上游构造函数里的 this.InitializeComponent()。
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = ThemeVariant.Dark;

        // 上游：UnhandledException += App_UnhandledException;
        Dispatcher.UIThread.UnhandledException += App_UnhandledException;

        // 上游：_gcTimer = new Timer(TimeSpan.FromSeconds(60)); _gcTimer.Elapsed += (_, _) => GC.Collect();
        _gcTimer = new DispatcherTimer(TimeSpan.FromSeconds(60), DispatcherPriority.Background, (_, _) => GC.Collect());
        _gcTimer.Start();
    }


    private void App_UnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
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
            foreach (DictionaryEntry item in e.Exception.Data)
            {
                sb.AppendLine($"{item.Key}: {item.Value}");
            }
        }
        using var fs = File.Open(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        using var sw = new StreamWriter(fs);
        sw.Write(sb);
    }


    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();

        if (args.Length > 0 && args[0].ToLower().StartsWith("lunarward://test/"))
        {
            new TestUrlProtocolWindow().Show();
            base.OnFrameworkInitializationCompleted();
            return;
        }

        // 自检不能卡在「选择数据目录」对话框上（无人值守），直接落到默认目录；
        // 正常启动仍走上游的完整环境检查（Welcome / NoPermission 窗口）。
        await AppConfig.CheckEnviromentAsync();

        if (args.Length > 0 && await HandleStartupAsync(args))
        {
            desktop.Shutdown();
            base.OnFrameworkInitializationCompleted();
            return;
        }

        // 上游 AppInstance.FindOrRegisterForKey("main") + RedirectActivationToAsync 的等价：
        // 第二个进程把消息发给已运行的实例后退出。
        if (Frameworks.SingleInstance.TryActivateExisting())
        {
            desktop.Shutdown();
            base.OnFrameworkInitializationCompleted();
            return;
        }
        Frameworks.SingleInstance.Listen(EnsureMainWindow);

        if (args.Contains("--hide") || Services.CaptureMode.StartHidden)
        {
            EnsureSystemTray();
        }
        else
        {
            EnsureMainWindow();
            desktop.MainWindow = m_MainWindow;
        }

        RunCaptureTooling(desktop);

        desktop.Exit += (_, _) =>
        {
            Frameworks.SingleInstance.Dispose();
            m_SystemTrayWindow?.Dispose();
            m_SystemTrayWindow = null;
        };

        base.OnFrameworkInitializationCompleted();
    }


    /// <summary>上游 <c>HandleStartupAsync</c>：rpc / playtime / startgame / url 协议四类不建窗口的子命令。</summary>
    private async Task<bool> HandleStartupAsync(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder().AddCommandLine(args).Build();
        if (args[0].ToLower() is "rpc")
        {
            RpcRunner.Run(args);
            return true;
        }
        if (args[0].ToLower() is "playtime")
        {
            int pid = config.GetValue<int>("pid");
            GameBiz biz = (GameBiz)config.GetValue<string>("biz");
            if (pid > 0)
            {
                var playtime = AppConfig.GetService<PlayTimeRecordService>();
                await playtime.LogPlayTimeAsync(biz, pid);
            }
            return true;
        }
        if (args[0].ToLower() is "startgame")
        {
            GameBiz biz = (GameBiz)config.GetValue<string>("biz");
            GameId? gameId = GameId.FromGameBiz(biz);
            if (gameId is not null)
            {
                await AppConfig.GetService<GameLauncherService>().StartGameAsync(gameId);
            }
            return true;
        }
        if (args[0].ToLower().StartsWith("lunarward://"))
        {
            if (await UrlProtocolService.HandleUrlProtocolAsync(args[0]))
            {
                return true;
            }
        }
        return false;
    }


    private MainWindow? m_MainWindow;

    private SystemTrayWindow? m_SystemTrayWindow;


    /// <summary>上游 <c>EnsureMainWindow</c>。</summary>
    public void EnsureMainWindow()
    {
        m_MainWindow ??= new MainWindow();
        m_MainWindow.Activate();
        m_MainWindow.Show();
    }


    /// <summary>上游 <c>EnsureSystemTray</c>：托盘常驻时窗口按需创建。</summary>
    public void EnsureSystemTray()
    {
        m_SystemTrayWindow ??= new SystemTrayWindow(m_MainWindow);
    }


    /// <summary>上游 <c>App.Exit</c>：先恢复手柄 Guide 键，再关闭窗口。</summary>
    /// <remarks>Avalonia 的 <see cref="Application"/> 没有同名无参成员，因此不需要 <c>new</c>（CS0109）。</remarks>
    public void Exit()
    {
        GamepadController.RestoreGamepadGuideButtonForGameBar();
        m_MainWindow?.Close();
        m_SystemTrayWindow?.Close();
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }



    // ---- 以下为本仓库的自检工具（--screenshot / --page / --window ...），不属于上游结构。 ----


    /// <summary>自检：切到 <c>--page</c> 指定页面，截图后退出。</summary>
    private static void RunCaptureTooling(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (!Services.CaptureMode.Enabled)
        {
            return;
        }

        if (CaptureWindow() is { } window)
        {
            if (Services.CaptureMode.Page is { Length: > 0 } page && ResolveCapturePage(page) is { } pageType)
            {
                window.MainViewHost.NavigateToForCapture(pageType);
            }
            if (Services.CaptureMode.StartHidden)
            {
                window.Hide();
            }
            Services.CaptureMode.Capture(window);
        }
        else if (Services.CaptureMode.QuitAfterMilliseconds > 0)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(Services.CaptureMode.QuitAfterMilliseconds);
                Dispatcher.UIThread.Post(() => desktop.Shutdown());
            });
        }
    }

    private static MainWindow? CaptureWindow()
        => Application.Current is App { m_MainWindow: { } window } ? window : null;


    /// <summary>把 <c>--page</c> 的字符串映射到页面类型（自检专用）。</summary>
    private static Type? ResolveCapturePage(string? page)
    {
        return page switch
        {
            "launcher" or null => typeof(Features.GameLauncher.GameLauncherPage),
            "game-setting" => typeof(Features.GameSetting.GameSettingPage),
            "screenshot" => typeof(Features.Screenshot.ScreenshotPage),
            "gacha" => typeof(Features.Gacha.GachaLogPage),
            "genshin-beyond" => typeof(Features.Gacha.GenshinBeyondGachaPage),
            "record" => typeof(Features.GameRecord.GameRecordPage),
            "self-query" => typeof(Features.SelfQuery.SelfQueryPage),
            "settings" => typeof(Features.Setting.SettingPage),
            "blank" => typeof(Features.ViewHost.BlankPage),
            _ => null,
        };
    }

}
