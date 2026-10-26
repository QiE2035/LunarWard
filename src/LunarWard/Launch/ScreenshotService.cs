using System.Text.RegularExpressions;
using LunarWard.Platform;

namespace LunarWard.Launch;

/// <summary>一张游戏内截图。</summary>
public sealed record GameScreenshot(string Path, DateTimeOffset CapturedAt, long SizeBytes)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}


/// <summary>
/// 截图目录发现与列举。
/// </summary>
/// <remarks>
/// <para>
/// 见 <c>docs/05-ROADMAP.md</c> §3.3："截图管理 —— 监视目录 + 文件操作"。
/// 上游 Starward 走 WIC（Windows Imaging Component）做缩略图；Linux 上
/// 我们<b>只做文件层面的发现与排序</b>，不引入图像库——Avalonia 自带位图解码，
/// 视图层直接 <c>new Bitmap(path)</c> 即可。
/// </para>
/// <para>
/// 截图目录是<b>游戏自己的</b>目录结构：
/// 原神/绝区零是 <c>&lt;game&gt;/ScreenShot</c>，星穹铁道是
/// <c>StarRail_Data/ScreenShots</c>（来自上游 <c>ScreenshotPage2.xaml.cs</c>）。
/// </para>
/// </remarks>
public sealed class ScreenshotService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp",
    };


    private static readonly Regex UidPrefix = new(@"^\d{6,}", RegexOptions.Compiled);


    /// <summary>截图相对游戏目录的位置。</summary>
    public static string RelativeDirectory(string gameKind) => gameKind switch
    {
        "hkrpg" => Path.Join("StarRail_Data", "ScreenShots"),
        _ => "ScreenShot",
    };


    /// <summary>截图的绝对目录。</summary>
    public static string GetDirectory(string gameDirectory, string gameKind)
        => Path.Join(gameDirectory, RelativeDirectory(gameKind));


    /// <summary>
    /// 列举截图（按时间倒序）。目录不存在时返回空列表。
    /// </summary>
    public IReadOnlyList<GameScreenshot> List(string gameDirectory, string gameKind, int max = 500)
    {
        var directory = GetDirectory(gameDirectory, gameKind);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            return new DirectoryInfo(directory)
                .EnumerateFiles()
                .Where(f => ImageExtensions.Contains(f.Extension))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(max)
                .Select(f => new GameScreenshot(
                    f.FullName,
                    new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero).ToLocalTime(),
                    f.Length))
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }


    /// <summary>
    /// 列举多个目录里的截图（去重后按时间倒序）。
    /// </summary>
    /// <remarks>
    /// 对应 Starward <c>ScreenshotPage2.xaml.cs</c>：游戏内目录 + <c>GetExternalScreenshotFolder</c>
    /// 里以 <c>;</c> 分隔的额外目录一起进图库，同名文件只取第一个遇到的那张。
    /// </remarks>
    public IReadOnlyList<GameScreenshot> List(IReadOnlyList<string> directories, int max = 500)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var all = new List<GameScreenshot>();
        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                foreach (var file in new DirectoryInfo(directory).EnumerateFiles())
                {
                    if (!ImageExtensions.Contains(file.Extension) || !seen.Add(file.Name))
                    {
                        continue;
                    }

                    all.Add(new GameScreenshot(
                        file.FullName,
                        new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero).ToLocalTime(),
                        file.Length));
                }
            }
            catch (Exception)
            {
                // 单个目录不可读时跳过，其余目录继续
            }
        }

        return all.OrderByDescending(x => x.CapturedAt).Take(max).ToList();
    }


    /// <summary>
    /// 从文件名前缀提取 UID（游戏截图命名形如 <c>100000000_2026-10-23_12-00-00.png</c>）。
    /// </summary>
    public static string? ExtractUid(string fileName)
    {
        var match = UidPrefix.Match(Path.GetFileNameWithoutExtension(fileName) ?? "");
        return match.Success ? match.Value : null;
    }


    /// <summary>
    /// 把截图复制到用户目标目录（保留原文件）。
    /// </summary>
    public string CopyTo(GameScreenshot screenshot, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var target = Path.Join(targetDirectory, screenshot.FileName);
        var unique = target;
        var index = 1;
        while (File.Exists(unique))
        {
            unique = Path.Join(targetDirectory,
                $"{Path.GetFileNameWithoutExtension(screenshot.FileName)}({index}){Path.GetExtension(screenshot.FileName)}");
            index++;
        }

        File.Copy(screenshot.Path, unique, overwrite: false);
        return unique;
    }


    /// <summary>
    /// 计算总占用（用于 UI 显示）。
    /// </summary>
    public (int Count, long TotalBytes) Summarize(string gameDirectory, string gameKind)
    {
        var items = List(gameDirectory, gameKind, max: int.MaxValue);
        return (items.Count, items.Sum(x => x.SizeBytes));
    }
}
