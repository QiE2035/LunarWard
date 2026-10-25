using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace LunarWard.App;

/// <summary>自检模式状态（<c>--screenshot</c>）。</summary>
internal static class CaptureMode
{
    public static string? Path { get; set; }

    /// <summary>要截图的旧窗口：<c>main</c>（Starward 外壳）/ settings / account / dailynote / gacha。</summary>
    public static string TargetWindow { get; set; } = "main";

    /// <summary>要切到的 Starward 页面（<c>launcher</c> 等），null = 启动器页。</summary>
    public static string? Page { get; set; }

    /// <summary>自检：账号记录页载入后选中第一行类型，便于核对右侧明细版式。</summary>
    public static bool SelfQuerySelect { get; set; }

    /// <summary>自检：账号记录页载入后打开「输入 URL」对话框并单独截图。</summary>
    public static bool SelfQueryInputUrl { get; set; }

    /// <summary>写入千星奇域颂愿的样例记录，供 <c>--page genshin-beyond</c> 截图渲染两张统计卡。</summary>
    public static bool BeyondSeed { get; set; }

    /// <summary>截图时展开「材料统计」Pane。</summary>
    public static bool BeyondPane { get; set; }

    /// <summary>截图时把两张统计卡切到第二个页签（5★ / 3★）。</summary>
    public static bool BeyondSecondTab { get; set; }

    /// <summary>
    /// 设置页的二级子页 tag（Starward 页面名）：<c>about</c> / <c>general</c> /
    /// <c>download</c> / <c>files</c> / <c>screenshot</c> / <c>hotkey</c> /
    /// <c>gamepad</c> / <c>advanced</c> / <c>toolbox</c>。配合 <c>--page settings</c> 使用。
    /// </summary>
    public static string? SettingsSection { get; set; }


    /// <summary>截「游戏设置」弹窗时点亮卸载警告行（<c>--uninstall-warning</c>）。</summary>
    public static bool UninstallWarning { get; set; }


    /// <summary>安装中悬停时弹出的「游戏信息 / 下载进度」卡片（<c>--install-info-popup</c>）。</summary>
    public static bool InstallInfoPopup { get; set; }

    /// <summary>指定档案 id。</summary>
    public static string? ProfileId { get; set; }

    /// <summary>截图前打开游戏选择层。</summary>
    public static bool OpenSelector { get; set; }


    /// <summary>游戏选择器第一张卡片悬停（<c>--selector-hover</c>，配 <c>--open-selector</c>）。</summary>
    public static bool SelectorHover { get; set; }


    /// <summary>自检：悬停卡片后弹出区服列表（<c>--selector-flyout</c>）。</summary>
    public static bool SelectorFlyout { get; set; }

    /// <summary>截图前展开左上角横排游戏图标条（模拟悬停当前游戏图标）。</summary>
    public static bool GameIcons { get; set; }

    /// <summary>截图前打开启动器页的每日便笺 Flyout。</summary>
    public static bool OpenDailyNote { get; set; }

    /// <summary>
    /// 自检：让右上角「通行证授权登录」按钮（GameAuthLoginButton）可见并套用一份失败态，
    /// 便于核对 Flyout 里的通行证 ID、错误行、重新认证与开关。
    /// </summary>
    public static bool AuthLogin { get; set; }

    /// <summary>截图前强制显示启动器底部背景切换条（模拟悬停）。</summary>
    public static bool ShowBackgroundBar { get; set; }

    /// <summary>截图前强制显示"拖放背景文件"提示条。</summary>
    public static bool ShowDragOverlay { get; set; }

    /// <summary>截图前打开「选择安装路径」对话框（自检安装器 UI）。</summary>
    public static bool InstallDialog { get; set; }

    /// <summary>截图前强制显示安装进度弹层（假数据，自检版式）。</summary>
    public static bool InstallProgress { get; set; }

    /// <summary>安装器自检走「更新游戏」语义（标题/按钮/完成文案）。</summary>
    public static bool InstallUpdate { get; set; }

    /// <summary>安装进度自检时假装鼠标悬停在按钮上（显示暂停/继续/取消）。</summary>
    public static bool InstallHover { get; set; }

    /// <summary>
    /// 自检时摆出预下载按钮的状态（不触发真实网络）：
    /// <c>idle</c> 预下载 / <c>downloading</c> 百分比 / <c>hover</c> 悬停弹层 / <c>finished</c> 完成后隐藏。
    /// </summary>
    public static string? Predownload { get; set; }

    /// <summary>工具箱战绩菜单索引（0 起），用于截取选中态/占位版式。</summary>
    public static string? Tab { get; set; }

    /// <summary>自检时的界面语言（<c>zh-CN</c> / <c>en-US</c>）。</summary>
    public static string? Language { get; set; }

    /// <summary>自检时把页面里的主 ScrollViewer 滚到该偏移（截长页下半部分）。</summary>
    public static double ScrollY { get; set; }

    /// <summary>抽卡页自检：<c>zzz</c> 造绝区零样例记录，<c>genshin</c> 造原神样例记录（不碰网络）。</summary>
    public static string? GachaSeed { get; set; }

    /// <summary>截抽卡页「删除指定时间段」对话框（自动灌 ZZZ 样例记录）。</summary>
    public static bool GachaDeleteDialog { get; set; }

    /// <summary>截 UIGF v4.2 导出 / 导入窗口（自动灌原神样例存档）。</summary>
    public static bool UigfWindow { get; set; }

    /// <summary>UIGF 窗口截图时切到导入页并灌一份样例文件。</summary>
    public static bool UigfImportPage { get; set; }

    /// <summary>自检：弹预下载确认框并截图（与 <see cref="Path"/> 配合）。</summary>
    public static bool PredownloadDialog { get; set; }


    /// <summary>DX12 详情引导弹窗（<c>--dx12-intro</c>）。</summary>
    public static bool Dx12IntroDialog { get; set; }

    /// <summary>自检：预下载确认框的「不可用 + 无补丁」状态。</summary>
    public static bool PredownloadDialogUnavailable { get; set; }

    /// <summary>自检：把样例记录按旧格式导出到该路径（验证 UIGF v3.0 / SRGF v1.0 信封）。</summary>
    public static string? GachaExport { get; set; }

    /// <summary>自检：把样例记录按 Excel 模板导出到该路径（<c>--gacha-excel</c>）。</summary>
    public static string? GachaExcel { get; set; }


    /// <summary>自检：读入旧格式文件并合并进页面列表（配合 <c>--gacha-export</c> 做往返验证）。</summary>
    public static string? GachaImport { get; set; }

    /// <summary>自检：把本地存档按 UIGF v4.2 导出到该路径。</summary>
    public static string? UigfExport { get; set; }

    /// <summary>截启动器「游戏设置」弹窗（配 --settings-section basic|arguments|background|packages）。</summary>
    public static bool LauncherSettingDialog { get; set; }

    /// <summary>截图页自检：把图库换成临时目录里的样例图（不碰用户游戏目录）。</summary>
    public static bool ScreenshotSeed { get; set; }

    /// <summary>截图页自检：把第一块磁贴置为 :pointerover，截悬停放大态。</summary>
    public static bool ScreenshotHover { get; set; }

    /// <summary>截「管理截图文件夹」对话框（自检 600 宽版式）。</summary>
    public static bool ScreenshotFolderDialog { get; set; }

    /// <summary>截图图片查看器时预先展开图库条。</summary>
    public static bool ImageViewerGallery { get; set; }

    /// <summary>截图图片查看器时预先展开右侧信息面板。</summary>
    public static bool ImageViewerInfo { get; set; }

    /// <summary>截图前把左栏展开（模拟鼠标悬停）。</summary>
    public static bool ExpandRail { get; set; }

    /// <summary>渲染前等待时间（毫秒）。</summary>
    public static int WaitMilliseconds { get; set; } = 1600;

    public static bool Enabled => Path is not null;


    private static bool _captureDatabasePrepared;

    private static string CaptureDatabaseFile
        => System.IO.Path.Join(System.IO.Path.GetTempPath(), "lunarward-capture", "capture.db");


    /// <summary>
    /// 自检用的独立数据库（<c>$TMP/lunarward-capture/capture.db</c>）：首次调用时删掉上一次的文件，
    /// 这样 <c>--gacha-seed</c> 等自检的样例记录不会写进用户的
    /// <c>~/.local/share/lunarward/LunarWardDatabase.db</c>；之后同进程内复用同一文件。
    /// </summary>
    public static LunarWard.Launch.Database.LunarWardDatabase CreateDatabase()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CaptureDatabaseFile)!);
        if (!_captureDatabasePrepared)
        {
            // 连接池可能还握着上一次的文件句柄：先清池再删（含 -wal / -shm），否则新库会建在已删除的 inode 上。
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (string suffix in new[] { "", "-wal", "-shm" })
            {
                if (File.Exists(CaptureDatabaseFile + suffix))
                {
                    File.Delete(CaptureDatabaseFile + suffix);
                }
            }
            _captureDatabasePrepared = true;
        }
        return new LunarWard.Launch.Database.LunarWardDatabase(CaptureDatabaseFile);
    }


    /// <summary>千星奇域存档（自检时同样落到临时库）。</summary>
    public static LunarWard.Launch.Database.GenshinBeyondGachaStore CreateBeyondStore()
        => new(CreateDatabase());


    /// <summary>
    /// 页面/服务应使用的数据库文件：自检时是临时库，否则是用户数据目录下的
    /// <c>LunarWardDatabase.db</c>。
    /// </summary>
    public static string ResolveDatabaseFile(string dataHome)
        => Enabled ? CaptureDatabaseFile : System.IO.Path.Join(dataHome, LunarWard.Launch.Database.LunarWardDatabase.FileName);


    /// <summary>渲染窗口到 PNG 并退出。</summary>
    public static void Capture(Window window)
    {
        if (Path is not { } path)
        {
            return;
        }

        DispatcherTimer.RunOnce(() =>
        {
            try
            {
                var size = new PixelSize(
                    Math.Max(1, (int)window.Bounds.Width),
                    Math.Max(1, (int)window.Bounds.Height));
                using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
                bitmap.Render(window);
                using (var stream = File.Create(path))
                {
                    bitmap.Save(stream, new PngBitmapEncoderOptions());
                }
                Console.WriteLine($"SCREENSHOT_OK {path} {size.Width}x{size.Height}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SCREENSHOT_FAIL {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown(0);
                }
            }
        }, TimeSpan.FromMilliseconds(WaitMilliseconds));
    }
}
