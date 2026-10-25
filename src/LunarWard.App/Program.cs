using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;

namespace LunarWard.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 自检开关：--screenshot <png> 打开主窗口、切到指定页面、渲染一帧、写 PNG 后退出。
        // 用于 CI/无人值守验证，不改变正常启动行为。
        //
        //   --screenshot <png>                 启动器页（默认）
        //   --screenshot-folder-dialog        打开「管理截图文件夹」对话框并截图
        //   --screenshot-seed / --screenshot-hover  截图页样例数据 / 首块磁贴悬停态
        //   --launcher-setting-dialog         启动器「游戏设置」弹窗（配 --settings-section basic|arguments|background|packages）
        //   --uninstall-warning               设置弹窗「基本信息」页把卸载警告行亮出来（配 --launcher-setting-dialog）
        //   --install-info-popup              安装中悬停的「游戏信息 / 下载进度」卡片（配 --install-progress）
        //   --selector-hover                  游戏选择器第一张卡片悬停态（配 --open-selector）
        //   --selector-flyout                 游戏选择器第一张卡片的区服列表（配 --open-selector）
        //   --gacha-seed zzz|genshin          抽卡页样例记录（不碰网络，便于截 ZZZ 统计卡）
        //   --gacha-delete-dialog             抽卡页「删除指定时间段」对话框（自动用 ZZZ 样例数据）
        //   --uigf-window                     UIGF v4.2 导出 / 导入窗口（自动用原神样例存档）
        //   --uigf-import                     配合 --uigf-window，直接切到导入页并灌样例文件
        //   --gacha-export <path>             把样例记录按 UIGF v3.0 / SRGF v1.0 导出到该路径
        //   --gacha-excel <path>              把样例记录按 Starward 的 Excel 模板导出到该路径
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
            CaptureMode.LauncherSettingDialog = args.Contains("--launcher-setting-dialog");
            CaptureMode.GachaSeed = ReadArgument(args, "--gacha-seed");
            CaptureMode.GachaDeleteDialog = args.Contains("--gacha-delete-dialog");
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
            if (int.TryParse(ReadArgument(args, "--scroll-y"), out var scrollY) && scrollY > 0)
            {
                CaptureMode.ScrollY = scrollY;
            }
            if (int.TryParse(ReadArgument(args, "--wait"), out var wait) && wait >= 0)
            {
                CaptureMode.WaitMilliseconds = wait;
            }
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }


    private static string? ReadArgument(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }


    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Starward 在 Windows 上靠系统字体回退把中文交给「微软雅黑」渲染；
            // Avalonia/Skia 不做隐式回退，这里显式给出回退链，效果与 Windows 一致。
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "Segoe UI",
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily("Microsoft YaHei UI") },
                    new FontFallback { FontFamily = new FontFamily("Noto Sans CJK SC") },
                    new FontFallback { FontFamily = new FontFamily("Segoe MDL2 Assets") },
                    new FontFallback { FontFamily = new FontFamily("Segoe Fluent Icons") },
                ],
            })
            .LogToTrace();
}
