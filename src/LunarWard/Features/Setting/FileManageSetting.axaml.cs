using LunarWard.Language;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Frameworks;
using LunarWard.Features.Database;
using Microsoft.Extensions.Logging;

namespace LunarWard.Features.Setting;

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
public sealed partial class FileManageSetting : PageBase
{

    public new event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private static Localization.AppStrings Strings => Localization.AppStrings.Instance;

    /// <summary>错误/状态行（上游各设置页末尾的 InfoBar 文本）。</summary>
    public string? Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    private string? _status;

    private static readonly ILogger<FileManageSetting> Log =
        AppConfig.GetLogger<FileManageSetting>();
    private readonly Func<string, string, Task<bool>> _confirm;


    public FileManageSetting(Func<string, string, Task<bool>> confirm)
    {
        SettingLocalization.EnsureConfigured();
        InitializeComponent();
        _confirm = confirm;

        ConfigHome = LunarWard.Platform.XdgPaths.Default.ConfigHome;
        DataHome = LunarWard.Platform.XdgPaths.Default.DataHome;
        CacheHome = LunarWard.Platform.XdgPaths.Default.CacheHome;
        // 上游的日志目录 = AppConfig.LogFile 所在目录（Serilog 正在写的那个）；
        // 取不到时退回 XDG 缓存下的 logs。
        LogDirectory = Path.GetDirectoryName(AppConfig.LogFile) ?? LunarWard.Platform.XdgPaths.Default.LogDirectory;
        ConfigFile = LunarWard.Platform.XdgPaths.Default.ConfigFile;

        _clearLauncherBackground = ReadClearLauncherBackground();
        RefreshCacheSize();

        // LunarWard 追加的「目录与文件」区块（Starward 没有这一块）：XDG 四个目录 + config.json
        DirectoryRows =
        [
            new SettingPathRowViewModel(Strings["Settings.Files.ConfigHome"], ConfigHome),
            new SettingPathRowViewModel(Lang.FileSettingPage_DataFolder, DataHome),
            new SettingPathRowViewModel(Strings["Settings.Files.CacheHome"], CacheHome),
            new SettingPathRowViewModel(Strings["Settings.Files.LogDir"], LogDirectory),
            new SettingPathRowViewModel(Strings["Settings.Files.ConfigFile"], ConfigFile),
        ];
        DataContext = this;
        BackupRequested += OnBackupRequested;
    }


    public string ConfigHome { get; }

    public string DataHome { get; }

    public string CacheHome { get; }

    public string LogDirectory { get; }

    public string ConfigFile { get; }


    /// <summary>追加区块「目录与文件」的五行（标签 + 绝对路径）。</summary>
    public IReadOnlyList<SettingPathRowViewModel> DirectoryRows { get; }


    // ── 缓存分类（Starward FileManageSetting.xaml:152-173 的五类）──────────────

    private string _logCacheSize = "";

    public string LogCacheSize
    {
        get => _logCacheSize;
        set => SetProperty(ref _logCacheSize, value);
    }

    private string _imageCacheSize = "";

    public string ImageCacheSize
    {
        get => _imageCacheSize;
        set => SetProperty(ref _imageCacheSize, value);
    }

    private string _browserCacheSize = "";

    public string BrowserCacheSize
    {
        get => _browserCacheSize;
        set => SetProperty(ref _browserCacheSize, value);
    }

    private string _gameCacheSize = "";

    public string GameCacheSize
    {
        get => _gameCacheSize;
        set => SetProperty(ref _gameCacheSize, value);
    }

    private string _launcherBackgroundSize = "";

    public string LauncherBackgroundSize
    {
        get => _launcherBackgroundSize;
        set => SetProperty(ref _launcherBackgroundSize, value);
    }

    private string _cacheSize = "";

    public string CacheSize
    {
        get => _cacheSize;
        set => SetProperty(ref _cacheSize, value);
    }

    private bool _isClearing;

    public bool IsClearing
    {
        get => _isClearing;
        set => SetProperty(ref _isClearing, value);
    }


    /// <summary>清理缓存时是否连启动器背景缓存一起删（Starward 的 CheckBox）。</summary>
    private bool _clearLauncherBackground;

    public bool ClearLauncherBackground
    {
        get => _clearLauncherBackground;
        set
        {
            if (SetProperty(ref _clearLauncherBackground, value))
            {
                OnClearLauncherBackgroundChanged(value);
            }
        }
    }


    private void OnClearLauncherBackgroundChanged(bool value) => SaveClearLauncherBackground(value);


    // ── 备份数据库（Starward FileManageSetting.xaml:72-108）──────────────────

    /// <summary>「备份数据库」被点击：由视图层弹保存对话框后回调 <see cref="BackupTo"/>。</summary>
    public event EventHandler? BackupRequested;


    private bool _isBackingUp;

    public bool IsBackingUp
    {
        get => _isBackingUp;
        set => SetProperty(ref _isBackingUp, value);
    }


    [RelayCommand]
    private void BackupDatabase() => BackupRequested?.Invoke(this, EventArgs.Empty);


    /// <summary>
    /// 把 <c>LunarWardDatabase.db</c> 的一致性快照写到 <paramref name="path"/>。
    /// </summary>
    /// <remarks>
    /// 走 <see cref="DatabaseService.BackupDatabase"/>（<c>VACUUM</c> + SQLite 官方
    /// <c>BackupDatabase</c> 热备份）：带事务、单文件、自动合并 WAL，不依赖 <c>sqlite3</c>
    /// 命令行。上游用 <c>File.Copy(StarwardDatabase.db)</c>，在 WAL 模式下可能拷到半个
    /// 事务；这里换成官方热备份方式，语义等价且更安全（已在类注释里记录偏差）。
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
            await Task.Run(() => DatabaseService.BackupDatabase(path)).ConfigureAwait(true);
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
            Lang.FileSettingPage_ClearCache,
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
            var backgroundDirectory = Path.GetFullPath(LunarWard.Platform.XdgPaths.Default.BackgroundDirectory);

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
            LunarWard.Platform.XdgPaths.Default.EnsureDirectories();

            var after = SettingShellActions.DirectorySize(CacheHome);
            Status = string.Format(
                CultureInfo.CurrentCulture,
                Strings["Settings.Files.ClearCacheDone"],
                SettingShellActions.Bytes(Math.Max(0, before - after)));

            RefreshCacheSize();
            Log.LogInformation($"设置页清理缓存：{before} → {after} 字节");
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
        var background = SettingShellActions.DirectorySize(LunarWard.Platform.XdgPaths.Default.BackgroundDirectory);
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


    // ── 「清理启动器背景文件」的读写（AppConfig 扩展字段，上游无）──────────────

    private bool ReadClearLauncherBackground()
    {
        try
        {
            return AppConfig.ClearLauncherBackground;
        }
        catch (Exception)
        {
            // 配置损坏：用默认值（与上游页面默认勾选一致）
            return true;
        }
    }


    private void SaveClearLauncherBackground(bool value)
    {
        try
        {
            AppConfig.ClearLauncherBackground = value;
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    /// <summary>
    /// 「备份数据库」：弹保存对话框，再把整库快照写到用户选的路径
    /// （模拟上游 <c>FileManageSetting</c> 的 FileSavePicker + File.Copy）。
    /// </summary>
    private async void OnBackupRequested(object? sender, EventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is not { CanSave: true } storage)
        {
            return;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Lang.SettingPage_BackupDatabase,
            SuggestedFileName = $"LunarWard_{DateTime.Now:yyyyMMdd_HHmmss}.db",
            DefaultExtension = "db",
            FileTypeChoices = [new FilePickerFileType("SQLite") { Patterns = ["*.db"] }],
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            await BackupToAsync(path);
        }
    }

}


/// <summary>追加区块「目录与文件」的一行：显示名 + 绝对路径。</summary>
public sealed record SettingPathRowViewModel(string Label, string Path);
