global using LunarWard.Language;
using LunarWard.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace LunarWard;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 移植自上游 Program.cs：进程级未处理异常先落盘，再让 CLR 终止进程。
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += (_, e) => CurrentDomain_UnhandledException(null, new UnhandledExceptionEventArgs(e.Exception, false));
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;

        // --version / --help：不启动窗口，也不读配置（打包脚本 / 桌面环境探测会调用）。
        if (args.Contains("--version") || args.Contains("-V"))
        {
            Console.WriteLine($"LunarWard {Version}");
            return;
        }

        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return;
        }

        // 游戏库扫描：打印找到的已安装游戏（含 Steam 库），不启动窗口。
        if (args.Contains("--scan-games"))
        {
            var scanner = new Services.GameScanner(LunarWard.Platform.XdgPaths.Default);
            var games = scanner.Scan();
            foreach (var game in games)
            {
                Console.WriteLine($"SCAN_GAME {game.Definition.Kind}|{game.Definition.Edition}|{game.Directory}");
            }

            Console.WriteLine($"SCAN_TOTAL {games.Count}");
            return;
        }

        // --start-hidden / --quit-after：开机自启到托盘、自检自动退出。
        CaptureMode.StartHidden = args.Contains("--start-hidden");
        if (args.Contains("--quit-after")
            && int.TryParse(ReadArgument(args, "--quit-after"), out int quitAfter)
            && quitAfter > 0)
        {
            CaptureMode.QuitAfterMilliseconds = quitAfter;
        }

        // 自检开关：--screenshot <png> 打开主窗口、切到指定页面、渲染一帧、写 PNG 后退出。
        // 用于 CI/无人值守验证，不改变正常启动行为。
        //
        //   --screenshot <png>                 启动器页（默认）
        //   --screenshot-folder-dialog        打开「管理截图文件夹」对话框并截图
        //   --screenshot-seed / --screenshot-hover  截图页样例数据 / 首块磁贴悬停态
        //   --launcher-setting-dialog         启动器「游戏设置」弹窗（配 --settings-section basic|arguments|background|packages）
        //   --uninstall-warning               设置弹窗「基本信息」页把卸载警告行亮出来（配 --launcher-setting-dialog）
        //   --close-window-dialog             关闭询问浮层（最小化到托盘 / 完全退出）
        //   --install-real <目录>             真实安装任务（官方 CDN）跑起来再截图，验证进度绑定
        //   --install-info-popup              安装中悬停的「游戏信息 / 下载进度」卡片（配 --install-progress）
        //   --selector-hover                  游戏选择器第一张卡片悬停态（配 --open-selector）
        //   --selector-flyout                 游戏选择器第一张卡片的区服列表（配 --open-selector）
        //   --gacha-seed zzz|genshin          抽卡页样例记录（不碰网络，便于截 ZZZ 统计卡）
        //   --gacha-delete-dialog             抽卡页「删除指定时间段」对话框（自动用 ZZZ 样例数据）
        //   --gacha-materials                 抽卡页右侧材料统计 Pane（自动用原神样例数据）
        //   --uigf-window                     UIGF v4.2 导出 / 导入窗口（自动用原神样例存档）
        //   --uigf-import                     配合 --uigf-window，直接切到导入页并灌样例文件
        //   --gacha-export <path>             把样例记录按 UIGF v3.0 / SRGF v1.0 导出到该路径
        //   --gacha-excel <path>              把样例记录按 Starward 的 Excel 模板导出到该路径
        //   --scan-games                      扫描游戏库并打印结果（含 Steam 的 steamapps/common）
        //   --start-hidden                    启动后藏到托盘（开机自启用，对应上游 --hide）
        //   --quit-after <ms>                 启动 N 毫秒后自动退出（自检用）
        //   --tray-test                       试建系统托盘并验证「关闭 = 隐藏」，打印 TRAY_OK / TRAY_FAIL
        //   --uigf-export <path>              把本地存档按 UIGF v4.2 导出到该路径（配 --gacha-seed）
        //   --predownload-dialog              启动器预下载确认框（配 --predownload-dialog-unavailable 看不可用态）
        //   --window image-viewer             截图片查看器（可加 --image-viewer-gallery / --image-viewer-info）
        //   --page launcher|game-setting|screenshot|gacha|toolbox|self-query|genshin-beyond|settings
        //   --settings-section <tag>           设置页的二级子页（tag 与 Starward 页面名一致）：
        //                                      about|general|download|files|screenshot|hotkey|
        //                                      gamepad|advanced|toolbox
        //   --profile <id>                     指定档案（如 genshin-cn）
        //   --wait <ms>                        渲染前等待（默认 1600，给网络素材留时间）
        //   --window <name>                    旧窗口自检（settings / account / dailynote / gacha）
        //   --version / -v                     打印版本号后退出（不开窗口）
        // --tray-test：验证 Avalonia TrayIcon 在当前桌面能否创建（截图流程顺带出一张图）。
        if (args.Contains("--toast-test"))
        {
            CaptureMode.ToastTest = true;
        }

        if (args.Contains("--tray-test"))
        {
            CaptureMode.Path = ReadArgument(args, "--screenshot") ?? "/tmp/tray-test.png";
            CaptureMode.TrayTest = true;
        }

        var index = Array.IndexOf(args, "--screenshot");
        if (index >= 0 && index + 1 < args.Length)
        {
            CaptureMode.Path = args[index + 1];
            CaptureMode.TargetWindow = ReadArgument(args, "--window") ?? "main";
            CaptureMode.Page = ReadArgument(args, "--page");
            CaptureMode.SettingsSection = ReadArgument(args, "--settings-section");
            CaptureMode.UninstallWarning = args.Contains("--uninstall-warning");
            CaptureMode.InstallInfoPopup = args.Contains("--install-info-popup");
            CaptureMode.ProfileId = ReadArgument(args, "--profile");
            CaptureMode.OpenSelector = args.Contains("--open-selector");
            CaptureMode.SelectorHover = args.Contains("--selector-hover");
            CaptureMode.SelectorFlyout = args.Contains("--selector-flyout");
            CaptureMode.GameIcons = args.Contains("--game-icons");
            CaptureMode.OpenDailyNote = args.Contains("--open-daily-note");
            CaptureMode.ShowBackgroundBar = args.Contains("--background-bar");
            CaptureMode.ShowDragOverlay = args.Contains("--drag-overlay");
            CaptureMode.InstallDialog = args.Contains("--install-dialog");
            CaptureMode.InstallProgress = args.Contains("--install-progress");
            CaptureMode.InstallRealPath = ReadArgument(args, "--install-real");
            CaptureMode.InstallUpdate = args.Contains("--install-update");
            CaptureMode.InstallHover = args.Contains("--install-hover");
            CaptureMode.Predownload = ReadArgument(args, "--predownload");
            CaptureMode.SelfQuerySelect = args.Contains("--selfquery-select");
            CaptureMode.SelfQueryInputUrl = args.Contains("--selfquery-url");
            CaptureMode.BeyondSeed = args.Contains("--beyond-seed");
            CaptureMode.BeyondPane = args.Contains("--beyond-pane");
            CaptureMode.BeyondSecondTab = args.Contains("--beyond-second-tab");
            CaptureMode.AuthLogin = args.Contains("--auth-login");
            CaptureMode.ScreenshotFolderDialog = args.Contains("--screenshot-folder-dialog");
            // 对话框现在是窗口内 DialogHost 浮层，由 MainWindow 的截图分支打开；
            // 老的 LauncherSettingDialog 开关不再设置（GameLauncherPage 里的旧分支失效）。
            CaptureMode.LauncherSettingOverlay = args.Contains("--launcher-setting-dialog");
            CaptureMode.GalleryDialog = args.Contains("--gallery-dialog");
            CaptureMode.PauseVideo = args.Contains("--pause-video");
            if (ReadArgument(args, "--switch-test") is { Length: > 0 } switchIndex
                && int.TryParse(switchIndex, out int parsedSwitchIndex))
            {
                CaptureMode.SwitchTest = parsedSwitchIndex;
            }
            CaptureMode.GachaSeed = ReadArgument(args, "--gacha-seed");
            CaptureMode.GachaDeleteDialog = args.Contains("--gacha-delete-dialog");
            CaptureMode.GachaMaterials = args.Contains("--gacha-materials");
            CaptureMode.UigfWindow = args.Contains("--uigf-window");
            CaptureMode.UigfImportPage = args.Contains("--uigf-import");
            CaptureMode.PredownloadDialog = args.Contains("--predownload-dialog");
            CaptureMode.Dx12IntroDialog = args.Contains("--dx12-intro");
            CaptureMode.PredownloadDialogUnavailable = args.Contains("--predownload-dialog-unavailable");
            CaptureMode.GachaExport = ReadArgument(args, "--gacha-export");
            CaptureMode.GachaExcel = ReadArgument(args, "--gacha-excel");
            CaptureMode.GachaImport = ReadArgument(args, "--gacha-import");
            CaptureMode.UigfExport = ReadArgument(args, "--uigf-export");
            CaptureMode.ScreenshotSeed = args.Contains("--screenshot-seed");
            CaptureMode.ScreenshotHover = args.Contains("--screenshot-hover");
            CaptureMode.ImageViewerGallery = args.Contains("--image-viewer-gallery");
            CaptureMode.ImageViewerInfo = args.Contains("--image-viewer-info");
            CaptureMode.Tab = ReadArgument(args, "--tab");
            CaptureMode.Language = ReadArgument(args, "--lang");
            CaptureMode.ExpandRail = args.Contains("--expand-rail");
            CaptureMode.BackgroundPath = ReadArgument(args, "--background");
            CaptureMode.CloseWindowDialog = args.Contains("--close-window-dialog");
            CaptureMode.DialogEscTest = args.Contains("--dialog-esc-test");
            if (int.TryParse(ReadArgument(args, "--scroll-y"), out var scrollY) && scrollY > 0)
            {
                CaptureMode.ScrollY = scrollY;
            }

            if (ReadArgument(args, "--scroll-target") is { Length: > 0 } scrollTarget)
            {
                CaptureMode.ScrollTarget = scrollTarget;
            }
            if (int.TryParse(ReadArgument(args, "--wait"), out var wait) && wait >= 0)
            {
                CaptureMode.WaitMilliseconds = wait;
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }


    /// <summary>版本号（唯一实现在 <see cref="AppVersion"/>）。</summary>
    internal static string Version => AppVersion.Display;


    private static void PrintUsage()
    {
        Console.WriteLine($"LunarWard {Version} — Linux 下的《原神》/《星穹铁道》/《绝区零》启动器");
        Console.WriteLine();
        Console.WriteLine("用法: lunarward [命令] [选项]");
        Console.WriteLine();
        Console.WriteLine("命令:");
        Console.WriteLine("  (无)                         打开启动器");
        Console.WriteLine("  startgame --biz <biz>        直接启动对应区服（不开窗口）");
        Console.WriteLine("  playtime --biz <biz> --pid <pid>  盯住外部启动的游戏进程，退出后补记时长");
        Console.WriteLine("  components list                  列出可用的 wine / dxvk 组件");
        Console.WriteLine("  components install <wine|dxvk> <名字>   安装指定组件");
        Console.WriteLine("  install-smoke                    安装器 30 秒下载烟测（临时目录）");
        Console.WriteLine("  \"lunarward://startgame/<biz>\"      URL 协议形式（桌面文件传给程序）");
        Console.WriteLine("  \"lunarward://playtime?biz=<biz>&pid=<pid>\"");
        Console.WriteLine();
        Console.WriteLine("选项:");
        Console.WriteLine("  --version, -V                输出版本号");
        Console.WriteLine("  --help, -h                   输出本帮助");
        Console.WriteLine("  --scan-games                 扫游戏库（含 Steam 的 steamapps/common）后退出");
        Console.WriteLine("  --start-hidden               启动后藏到系统托盘（开机自启用）");
        Console.WriteLine();
        Console.WriteLine("自检截图开关见 https://github.com/ …/README.md 的「自检与截图」一节。");
    }


    private static string? ReadArgument(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }


    /// <summary>
    /// 上游 <c>Program.CurrentDomain_UnhandledException</c> 的等价实现（日志文件名与格式保持一致）。
    /// </summary>
    private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            string logFile = LunarWard.AppConfig.LogFile;
            if (string.IsNullOrWhiteSpace(logFile))
            {
                string logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunarWard", "log");
                Directory.CreateDirectory(logFolder);
                logFile = Path.Combine(logFolder, $"LunarWard_{DateTime.Now:yyMMdd}.log");
            }
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Program Crash:");
            sb.AppendLine(e.ExceptionObject?.ToString());
            if (e.ExceptionObject is Exception { Data.Count: > 0 } ex)
            {
                foreach (System.Collections.DictionaryEntry item in ex.Data)
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
            // 崩溃日志自身失败时不再抛出，避免掩盖原始异常。
        }
    }


    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // 拉丁字形沿用 Starward 的 Segoe UI；缺字时回退到随应用分发的 Inter，
            // 保证未安装 Segoe UI 的机器上度量仍然稳定。
            .WithInterFont()
            // Starward 在 Windows 上靠系统字体回退把中文交给「微软雅黑」渲染；
            // Avalonia/Skia 不做隐式回退，这里显式给出回退链。
            //
            // 顺序有意义：CJK 优先 Noto Sans CJK SC（全字重），再退到微软雅黑。
            // 后者在 Linux 上通常来自 WPS 字体包且只有 Regular，标题会发虚、
            // 与 Windows 观感不符（见 docs/09-LINUX-DIVERGENCES.md）。
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "Segoe UI",
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily("Inter") },
                    new FontFallback { FontFamily = new FontFamily("Noto Sans CJK SC") },
                    new FontFallback { FontFamily = new FontFamily("Microsoft YaHei UI") },
                    new FontFallback { FontFamily = new FontFamily("Noto Sans CJK JP") },
                    new FontFallback { FontFamily = new FontFamily("Segoe MDL2 Assets") },
                    new FontFallback { FontFamily = new FontFamily("Segoe Fluent Icons") },
                ],
            })
            .LogToTrace();
}
