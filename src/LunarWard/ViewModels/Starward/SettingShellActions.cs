using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 设置子页共用的"打开 / 复制路径 / 体积格式化"小工具。
/// </summary>
/// <remarks>
/// 集中一处，避免每个子页各写一份 <c>xdg-open</c> 调用。失败只返回错误文本，
/// 不抛异常（设置页不应该因为目录不存在就崩）。
/// </remarks>
internal static class SettingShellActions
{
    /// <summary>用桌面默认程序打开一个路径或 <c>.desktop</c> 文件。</summary>
    public static string? TryOpen(string path)
    {
        try
        {
            var info = new ProcessStartInfo { FileName = "xdg-open", UseShellExecute = false };
            info.ArgumentList.Add(path);
            using var process = Process.Start(info);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }


    /// <summary>把文本写进主窗口剪贴板。</summary>
    public static async Task<string?> TryCopyAsync(string text)
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            {
                await window.Clipboard!.SetTextAsync(text);
                return null;
            }

            return "no window";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }


    /// <summary>递归计算目录占用；不存在或无权访问时返回 0。</summary>
    public static long DirectorySize(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }

            long total = 0;
            foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    total += file.Length;
                }
                catch (IOException)
                {
                    // 文件正在写/已删除：跳过
                }
            }

            return total;
        }
        catch (Exception)
        {
            return 0;
        }
    }


    /// <summary>字节数转人类可读（1 KiB 起）。</summary>
    public static string Bytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        double value = bytes;
        string[] units = ["KiB", "MiB", "GiB", "TiB"];
        var unit = -1;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }


    /// <summary>主窗口所属的 <see cref="IStorageProvider"/>（可能为 null，无窗口时）。</summary>
    public static IStorageProvider? StorageProvider
        => Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? window.StorageProvider
            : null;


    /// <summary>桌面入口（.desktop）的候选路径，按优先级排列。</summary>
    public static IEnumerable<string> DesktopEntryCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome))
        {
            dataHome = Path.Join(home, ".local", "share");
        }

        yield return Path.Join(dataHome, "applications", "lunarward.desktop");
        yield return "/usr/share/applications/lunarward.desktop";
        yield return "/usr/local/share/applications/lunarward.desktop";
        yield return Path.Join(AppContext.BaseDirectory, "lunarward.desktop");
    }


    /// <summary>找到已安装的 <c>lunarward.desktop</c>，找不到返回 <c>null</c>。</summary>
    public static string? FindDesktopEntry() => DesktopEntryCandidates().FirstOrDefault(File.Exists);
}
