using LunarWard.Launch;

namespace LunarWard.Services;

/// <summary>
/// 卸载游戏（复刻 Starward <c>GameInstaller.UninstallGameAsync</c> 的 Linux 等价物）。
/// </summary>
/// <remarks>
/// 对应源码 <c>src/Starward/Features/GameInstall/GameInstaller.cs</c> 的
/// <c>UninstallGameAsync(GameBiz gameBiz)</c>：
/// <list type="number">
/// <item>按「游戏目录是否等于根目录 / 数据目录在游戏目录里 / 程序目录在游戏目录里」做安全检查，命中就抛异常；</item>
/// <item>把游戏目录里的截图复制到用户截图目录（源码是 <c>~/Pictures/&lt;游戏名&gt;/Screenshots/&lt;exe 名&gt;</c>，同目录硬链接优先）；</item>
/// <item>递归删除游戏目录。</item>
/// </list>
/// 差异（记入 parity 文档）：源码读注册表里的安装目录并清缓存目录，这里用配置里的安装目录，缓存目录由调用方处理。
/// </remarks>
public static class GameUninstaller
{

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp",
    };

    /// <summary>截图备份目标：<c>~/Pictures/LunarWard</c>（与截图页「导出」同一目录）。</summary>
    public static string GetScreenshotBackupDirectory() => Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "LunarWard");


    public static bool IsInside(string? child, string parent)
    {
        if (string.IsNullOrWhiteSpace(child) || string.IsNullOrWhiteSpace(parent))
        {
            return false;
        }

        var childFull = Path.GetFullPath(child);
        var parentFull = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar);
        if (childFull.Equals(parentFull, StringComparison.Ordinal))
        {
            return true;
        }

        return childFull.StartsWith(parentFull + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }


    /// <summary>
    /// 卸载前的安全检查（对应源码 <c>UninstallGameAsync</c> 开头三段判断）。
    /// 返回 <c>null</c> 表示可以卸载，否则是给用户看的原因。
    /// </summary>
    /// <param name="installPath">游戏安装目录。</param>
    /// <param name="dataHome">LunarWard 数据目录。</param>
    /// <param name="applicationDirectory">LunarWard 程序目录。</param>
    public static string? Validate(string? installPath, string? dataHome, string? applicationDirectory)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return ErrorMessage("UninstallNoPath");
        }

        string full;
        try
        {
            full = Path.GetFullPath(installPath);
        }
        catch (Exception)
        {
            return ErrorMessage("UninstallBadPath");
        }

        if (!Directory.Exists(full))
        {
            return ErrorMessage("UninstallNotInstalled");
        }

        var root = Path.GetPathRoot(full);
        if (!string.IsNullOrEmpty(root)
            && full.TrimEnd(Path.DirectorySeparatorChar).Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
        {
            return ErrorMessage("UninstallRefuseRoot");
        }

        if (IsInside(dataHome, full) && !string.IsNullOrWhiteSpace(dataHome))
        {
            // 数据目录在游戏目录里 → 删游戏会把配置一起删掉
            return ErrorMessage("UninstallDataInsideGame");
        }

        if (IsInside(applicationDirectory, full) && !string.IsNullOrWhiteSpace(applicationDirectory))
        {
            return ErrorMessage("UninstallAppInsideGame");
        }

        return null;
    }


    /// <summary>把游戏目录里的截图复制进 <paramref name="backupDirectory"/>（已存在同名文件跳过）。</summary>
    /// <returns>复制的文件数。</returns>
    public static int BackupScreenshots(string installPath, string gameKind, string backupDirectory, CancellationToken cancellationToken = default)
    {
        var source = Path.Join(installPath, ScreenshotService.RelativeDirectory(gameKind));
        if (!Directory.Exists(source))
        {
            return 0;
        }

        Directory.CreateDirectory(backupDirectory);
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(file);
            if (!ImageExtensions.Contains(extension))
            {
                continue;
            }

            var target = Path.Join(backupDirectory, Path.GetFileName(file));
            if (File.Exists(target))
            {
                continue;
            }

            try
            {
                File.Copy(file, target);
                count++;
            }
            catch (Exception)
            {
                // 单张失败不影响卸载（源码同样吞掉异常）
            }
        }

        return count;
    }


    /// <summary>递归删除目录，进度按文件数报告（0~100）。</summary>
    public static void DeleteDirectory(string installPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var files = new List<string>(Directory.EnumerateFiles(installPath, "*", SearchOption.AllDirectories));
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch (Exception)
            {
                // 只读/占用文件留到最后由 Directory.Delete 决策
            }

            if (i % 64 == 0)
            {
                progress?.Report(files.Count == 0 ? 100 : i * 100d / files.Count);
            }
        }

        progress?.Report(100);
        Directory.Delete(installPath, true);
    }


    /// <summary>
    /// 卸载整个游戏目录。<paramref name="gameKind"/> 用于定位截图目录，
    /// <paramref name="screenshotBackupDirectory"/> 为空时不备份截图。
    /// </summary>
    public static int Uninstall(string installPath, string gameKind, string? screenshotBackupDirectory,
                                IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var backedUp = 0;
        if (!string.IsNullOrWhiteSpace(screenshotBackupDirectory))
        {
            backedUp = BackupScreenshots(installPath, gameKind, screenshotBackupDirectory, cancellationToken);
        }

        DeleteDirectory(installPath, progress, cancellationToken);
        return backedUp;
    }


    private static string ErrorMessage(string key) => key switch
    {
        "UninstallNoPath" => "游戏目录为空，无法卸载。",
        "UninstallBadPath" => "游戏目录无效，无法卸载。",
        "UninstallNotInstalled" => "游戏目录不存在，可能已经被删除。",
        "UninstallRefuseRoot" => "拒绝卸载：游戏目录是文件系统根目录。",
        "UninstallDataInsideGame" => "拒绝卸载：LunarWard 数据目录在游戏目录里，卸载会连带删除配置。",
        "UninstallAppInsideGame" => "拒绝卸载：LunarWard 程序目录在游戏目录里。",
        _ => key,
    };

}
