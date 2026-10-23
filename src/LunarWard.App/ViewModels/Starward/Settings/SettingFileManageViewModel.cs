using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 文件管理页（复刻 Starward <c>FileManageSetting.xaml</c>）。
/// </summary>
/// <remarks>
/// Starward 有"改数据目录 / 备份数据库 / 删除所有设置"；Linux 上目录由 XDG 环境变量
/// 决定（改目录请改 <c>XDG_*</c>），因此这里展示四类目录 + 配置文件 + 清理缓存。
/// 清理会真的删 <c>CacheHome</c> 下的内容（保留当天日志），并要二次确认。
/// </remarks>
public sealed partial class SettingFileManageViewModel : SettingSectionViewModel
{
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

        RefreshCacheSize();
    }


    public string ConfigHome { get; }

    public string DataHome { get; }

    public string CacheHome { get; }

    public string LogDirectory { get; }

    public string ConfigFile { get; }


    [ObservableProperty]
    private string _cacheSize = "";


    [ObservableProperty]
    private string? _status;


    [ObservableProperty]
    private bool _isClearing;


    /// <summary>四类目录的浏览按钮共用一个命令（按钮的 CommandParameter 是路径）。</summary>
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
            var today = $"lunarward-{DateTime.Now:yyyy-MM-dd}";

            foreach (var file in Directory.EnumerateFiles(CacheHome))
            {
                // 当天的日志还被 Serilog 打开着，删了会让后续日志消失：明确保留
                if (Path.GetFileName(file).StartsWith(today, StringComparison.Ordinal))
                {
                    continue;
                }

                TryDeleteFile(file);
            }

            foreach (var dir in Directory.EnumerateDirectories(CacheHome))
            {
                TryDeleteDirectory(dir);
            }

            // 删空的子目录后重建目录结构（背景/日志/缓存目录要还在）
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
        var bytes = SettingShellActions.DirectorySize(CacheHome);
        CacheSize = string.Format(
            CultureInfo.CurrentCulture,
            Strings["Settings.Files.CacheSize"],
            SettingShellActions.Bytes(bytes));
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
}
