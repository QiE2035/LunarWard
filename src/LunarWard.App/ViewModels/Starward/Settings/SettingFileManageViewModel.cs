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
            var before = SettingShellActions.DirectorySize(CacheHome ?? "");
            var logDirectory = LogDirectory ?? "";

            // 目录结构（logs / background / bilibili…）要保留，只清内容：
            // 当天日志还被 Serilog 打开着，删掉会让本次会话之后的日志消失。
            foreach (var dir in Directory.EnumerateDirectories(CacheHome))
            {
                if (string.Equals(Path.GetFullPath(dir), Path.GetFullPath(logDirectory), StringComparison.Ordinal))
                {
                    ClearLogDirectory(dir);
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
        var bytes = SettingShellActions.DirectorySize(CacheHome);
        CacheSize = string.Format(
            CultureInfo.CurrentCulture,
            Strings["Settings.Files.CacheSize"],
            SettingShellActions.Bytes(bytes));
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
}
