using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;
using LunarWard.Launch.Database;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 文件管理页（1:1 复刻 Starward <c>FileManageSetting.xaml</c>）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 的顺序（<c>FileManageSetting.xaml</c>）：数据文件夹（18-69）→ 备份数据库
/// （72-108）→ 删除所有设置（111-123）→ 日志（126-142）→ 缓存（145-199）。
/// 本页严格按这个顺序排；Linux 上没有「改数据目录 / 备份 SQLite / 一键删设置」，
/// 这三个控件保留位置并禁用，旁边写原因。
/// </para>
/// <para>
/// 「日志 / 缓存」两块真的可用：缓存按 Starward 的五类（日志、图片、浏览器、游戏资源、
/// 启动器背景）分别统计占用；清理缓存会真的删 <c>CacheHome</c> 下的内容（保留当天日志），
/// 并按「清理启动器背景文件」开关决定是否连背景缓存一起删。
/// </para>
/// <para>
/// LunarWard 追加的「目录与文件」区块排在 Starward 全部条目之后，列出 XDG 的四个目录
/// 与 config.json（Linux 上改目录要改 <c>XDG_*</c> 环境变量，不是界面）。
/// </para>
/// </remarks>
public sealed partial class SettingFileManageViewModel : SettingSectionViewModel
{
    /// <summary>「清理启动器背景文件」的持久化键（配置 <c>launcher</c> 段的未知字段区）。</summary>
    /// <remarks>
    /// Starward 把它存在 <c>AppConfig.ClearLauncherBackground</c>；LunarWard 的
    /// <c>LauncherSettings</c> 没有这个字段（不改上游配置类），用 <c>ExtensionData</c> 承接。
    /// </remarks>
    private const string ClearLauncherBackgroundKey = "clear_launcher_background";

    private readonly Func<string, string, Task<bool>> _confirm;


    public SettingFileManageViewModel(
        PageContext context,
        Func<string, string, Task<bool>> confirm,
        bool showDivider = false)
        : base(context, SettingSectionKind.FileManage, "Settings.FileManagement", "\uE8F1", showDivider)
    {
        _confirm = confirm;

        ConfigHome = Services.Paths.ConfigHome;
        DataHome = Services.Paths.DataHome;
        CacheHome = Services.Paths.CacheHome;
        LogDirectory = Services.Paths.LogDirectory;
        ConfigFile = Services.Paths.ConfigFile;

        _clearLauncherBackground = ReadClearLauncherBackground();
        RefreshCacheSize();

        // LunarWard 追加的「目录与文件」区块（Starward 没有这一块）：XDG 四个目录 + config.json
        DirectoryRows =
        [
            new SettingPathRowViewModel(Strings["Settings.Files.ConfigHome"], ConfigHome),
            new SettingPathRowViewModel(Strings["Settings.Files.DataHome"], DataHome),
            new SettingPathRowViewModel(Strings["Settings.Files.CacheHome"], CacheHome),
            new SettingPathRowViewModel(Strings["Settings.Files.LogDir"], LogDirectory),
            new SettingPathRowViewModel(Strings["Settings.Files.ConfigFile"], ConfigFile),
        ];
    }


    public string ConfigHome { get; }

    public string DataHome { get; }

    public string CacheHome { get; }

    public string LogDirectory { get; }

    public string ConfigFile { get; }


    /// <summary>追加区块「目录与文件」的五行（标签 + 绝对路径）。</summary>
    public IReadOnlyList<SettingPathRowViewModel> DirectoryRows { get; }


    // ── 缓存分类（Starward FileManageSetting.xaml:152-173 的五类）──────────────

    [ObservableProperty]
    private string _logCacheSize = "";

    [ObservableProperty]
    private string _imageCacheSize = "";

    [ObservableProperty]
    private string _browserCacheSize = "";

    [ObservableProperty]
    private string _gameCacheSize = "";

    [ObservableProperty]
    private string _launcherBackgroundSize = "";

    [ObservableProperty]
    private string _cacheSize = "";

    [ObservableProperty]
    private bool _isClearing;


    /// <summary>清理缓存时是否连启动器背景缓存一起删（Starward 的 CheckBox）。</summary>
    [ObservableProperty]
    private bool _clearLauncherBackground;


    partial void OnClearLauncherBackgroundChanged(bool value) => SaveClearLauncherBackground(value);


    // ── 备份数据库（Starward FileManageSetting.xaml:72-108）──────────────────

    /// <summary>「备份数据库」被点击：由视图层弹保存对话框后回调 <see cref="BackupTo"/>。</summary>
    public event EventHandler? BackupRequested;


    [ObservableProperty]
    private bool _isBackingUp;


    [RelayCommand]
    private void BackupDatabase() => BackupRequested?.Invoke(this, EventArgs.Empty);


    /// <summary>
    /// 把 <c>LunarWardDatabase.db</c> 的一致性快照写到 <paramref name="path"/>。
    /// </summary>
    /// <remarks>
    /// 用 SQLite 的 <c>VACUUM INTO</c>（<see cref="LunarWard.Launch.Database.LunarWardDatabase.BackupTo"/>）：
    /// 带事务、单文件、自动合并 WAL，不依赖 <c>sqlite3</c> 命令行。上游用
    /// <c>File.Copy(StarwardDatabase.db)</c>，在 WAL 模式下可能拷到半个事务；
    /// 这里换成官方热备份方式，语义等价且更安全（已在类注释里记录偏差）。
    /// </remarks>
    public async Task BackupToAsync(string path)
    {
        if (IsBackingUp)
        {
            return;
        }

        IsBackingUp = true;
        Status = Strings["Settings.Files.BackingUp"];
        try
        {
            var database = new LunarWardDatabase(Path.Join(DataHome, LunarWardDatabase.FileName));
            await Task.Run(() => database.BackupTo(path)).ConfigureAwait(true);
            Status = string.Format(CultureInfo.CurrentCulture, Strings["Settings.Files.BackupDone"], path);
        }
        catch (Exception ex)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings["Settings.Files.BackupFailed"], ex.Message);
        }
        finally
        {
            IsBackingUp = false;
        }
    }


    // ── 目录按钮 ────────────────────────────────────────────────────────────

    /// <summary>目录浏览按钮共用一个命令（按钮的 CommandParameter 是路径）。</summary>
    [RelayCommand]
    private void OpenPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        Status = SettingShellActions.TryOpen(path) is { } error
            ? string.Format(CultureInfo.CurrentCulture, Strings["Settings.Common.OpenFailed"], error)
            : null;
    }


    [RelayCommand]
    private void OpenConfigFolder() => OpenPath(ConfigHome);


    [RelayCommand]
    private async Task CopyPathAsync(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        Status = await SettingShellActions.TryCopyAsync(path).ConfigureAwait(true) is { } error
            ? error
            : Strings["Settings.Common.Copied"];
    }


    // ── 清理缓存 ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        if (IsClearing)
        {
            return;
        }

        var confirmed = await _confirm(
            Strings["Settings.Files.ClearCacheTitle"],
            string.Format(CultureInfo.CurrentCulture, Strings["Settings.Files.ClearCacheBody"], CacheHome)).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        IsClearing = true;
        Status = Strings["Settings.Files.Clearing"];
        try
        {
            var before = SettingShellActions.DirectorySize(CacheHome);
            var logDirectory = Path.GetFullPath(LogDirectory);
            var backgroundDirectory = Path.GetFullPath(Services.Paths.BackgroundDirectory);

            // 目录结构（logs / background / bilibili…）要保留，只清内容：
            // 当天日志还被 Serilog 打开着，删掉会让本次会话之后的日志消失。
            foreach (var dir in Directory.EnumerateDirectories(CacheHome))
            {
                var full = Path.GetFullPath(dir);
                if (string.Equals(full, logDirectory, StringComparison.Ordinal))
                {
                    ClearLogDirectory(dir);
                    continue;
                }

                // Starward 的「清理启动器背景文件」开关：不勾就跳过背景缓存
                if (!ClearLauncherBackground
                    && string.Equals(full, backgroundDirectory, StringComparison.Ordinal))
                {
                    continue;
                }

                TryDeleteDirectory(dir);
            }

            foreach (var file in Directory.EnumerateFiles(CacheHome))
            {
                TryDeleteFile(file);
            }

            // 目录可能被整棵删掉：重建（背景/日志/缓存目录要还在）
            (Services.Paths as Platform.XdgPaths)?.EnsureDirectories();

            var after = SettingShellActions.DirectorySize(CacheHome);
            Status = string.Format(
                CultureInfo.CurrentCulture,
                Strings["Settings.Files.ClearCacheDone"],
                SettingShellActions.Bytes(Math.Max(0, before - after)));

            RefreshCacheSize();
            Log.Info($"设置页清理缓存：{before} → {after} 字节");
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsClearing = false;
        }
    }


    private void RefreshCacheSize()
    {
        var cacheHome = CacheHome;
        var log = SettingShellActions.DirectorySize(LogDirectory);
        // 「图片 / 游戏资源」在 LunarWard 共用 starward 缓存（远端素材），
        // 这里把图片算成素材缓存，游戏资源项如实显示 0（LunarWard 没有游戏资源缓存）。
        var assets = SettingShellActions.DirectorySize(Path.Join(cacheHome, "starward"));
        var browser = SettingShellActions.DirectorySize(Path.Join(cacheHome, "bilibili"));
        var background = SettingShellActions.DirectorySize(Services.Paths.BackgroundDirectory);
        var total = SettingShellActions.DirectorySize(cacheHome);

        LogCacheSize = SettingShellActions.Bytes(log);
        ImageCacheSize = SettingShellActions.Bytes(assets);
        BrowserCacheSize = SettingShellActions.Bytes(browser);
        GameCacheSize = SettingShellActions.Bytes(0);
        LauncherBackgroundSize = SettingShellActions.Bytes(background);
        CacheSize = string.Format(
            CultureInfo.CurrentCulture,
            Strings["Settings.Files.CacheSize"],
            SettingShellActions.Bytes(total));
    }


    /// <summary>清日志目录：保留当天的滚动文件，其余删除。</summary>
    /// <remarks>
    /// Serilog 的文件名是 <c>lunarward-20261023.log</c>（无短横线的日期），而
    /// <see cref="LogService.LogFile"/> 用的是 <c>lunarward-2026-10-23.log</c>；两种都要认。
    /// 另外当天写过的文件也一并保留，避免跨零点后刚写的日志被删。
    /// </remarks>
    private void ClearLogDirectory(string directory)
    {
        var now = DateTime.Now;
        var dashed = $"lunarward-{now:yyyy-MM-dd}";
        var plain = $"lunarward-{now:yyyyMMdd}";
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(dashed, StringComparison.Ordinal)
                    || name.StartsWith(plain, StringComparison.Ordinal))
                {
                    continue;
                }

                TryDeleteFile(file);
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                TryDeleteDirectory(child);
            }
        }
        catch (Exception)
        {
            // 无权限/目录消失：跳过
        }
    }


    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // 被占用/无权限：跳过，不中断整体清理
        }
    }


    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception)
        {
            // 同上
        }
    }


    // ── 「清理启动器背景文件」的读写（ExtensionData）────────────────────────

    private bool ReadClearLauncherBackground()
    {
        try
        {
            if (Services.Config.Launcher.ExtensionData?.TryGetValue(ClearLauncherBackgroundKey, out var element) == true
                && element.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return element.GetBoolean();
            }
        }
        catch (Exception)
        {
            // 配置损坏：用默认值
        }

        // Starward 默认勾选（AppConfig.ClearLauncherBackground 默认 true）
        return true;
    }


    private void SaveClearLauncherBackground(bool value)
    {
        try
        {
            Services.Config.Launcher.ExtensionData ??= [];
            Services.Config.Launcher.ExtensionData[ClearLauncherBackgroundKey] =
                JsonSerializer.SerializeToElement(value);
            Services.Config.Save(Services.Paths.ConfigFile);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }
}


/// <summary>追加区块「目录与文件」的一行：显示名 + 绝对路径。</summary>
public sealed record SettingPathRowViewModel(string Label, string Path);
