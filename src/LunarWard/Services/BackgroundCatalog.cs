using LunarWard.Core.HoYoPlay;

namespace LunarWard.Services;

/// <summary>背景条目来源。</summary>
public enum BackgroundKind
{
    /// <summary>官方版本背景（接口返回的图或视频）。</summary>
    Official,

    /// <summary>本地背景目录里的图片/视频。</summary>
    Custom,
}


/// <summary>
/// 一条可切换的背景。图片与视频可以同时存在：视频不可用（VLC 缺失/关闭动态背景）时
/// 用 <see cref="ImageSource"/> 兜底，等价于 Starward <c>GameBackground.Background</c> 与 <c>Video</c> 的关系。
/// </summary>
/// <param name="Kind">来源（官方/本地）。</param>
/// <param name="ImageSource">图片：本地绝对路径或 https URL（可为 null，纯视频条目也有兜底图）。</param>
/// <param name="VideoSource">视频：本地绝对路径或 https URL。</param>
/// <param name="Title">显示名。</param>
/// <param name="ThemeSource">版本亮点海报（含 logo 与标题文字，PNG 透明底）：叠在背景图/视频帧之上。</param>
public sealed record BackgroundEntry(BackgroundKind Kind, string? ImageSource, string? VideoSource, string Title, string? ThemeSource = null)
{
    public bool IsVideo => !string.IsNullOrWhiteSpace(VideoSource);

    /// <summary>代表值（图库/复制/另存为用它取文件）。视频优先。</summary>
    public string Value => VideoSource ?? ImageSource ?? "";
}


/// <summary>
/// 背景目录：把 HoYoPlay <c>getAllGameBasicInfo</c> 的 <c>backgrounds</c>、游戏素材里的版本海报、
/// 本地背景目录合并成可切换列表。
/// </summary>
/// <remarks>
/// Starward 侧对应 <c>BackgroundService.GetGameBackgroundsAsync</c>（API 背景 + <c>Display.Background</c>
/// 海报 + 自定义文件）与 <c>GameLauncherPage</c> 的切换条。这里保持纯函数式映射，方便单测。
/// </remarks>
public static class BackgroundCatalog
{
    /// <summary>Starward <c>BackgroundService.FileIsSupportedVideo</c>：mp4 / mkv / webm。</summary>
    public static bool IsSupportedVideo(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var path = source;
        var query = path.IndexOfAny(['?', '#']);
        if (query >= 0)
        {
            path = path[..query];
        }

        return Path.GetExtension(path).ToLowerInvariant() is ".mp4" or ".mkv" or ".webm";
    }


    /// <summary>
    /// 官方接口背景映射：<c>type == BACKGROUND_TYPE_VIDEO</c> 生成视频条目（用 <c>background</c> 当兜底图），
    /// 其余生成图片条目。空 URL 的条目丢弃，保持接口顺序。
    /// </summary>
    public static IReadOnlyList<BackgroundEntry> FromApi(GameBackgroundInfo? info)
    {
        var result = new List<BackgroundEntry>();
        foreach (var background in info?.Backgrounds ?? [])
        {
            if (background is null)
            {
                continue;
            }

            var image = background.Background?.Url;
            var video = background.Video?.Url;
            // 上游 AppBackground 会把 theme（logo/标题文字海报）叠在背景或视频帧上。
            var theme = background.Theme?.Url;

            if (string.Equals(background.Type, GameBackground.BACKGROUND_TYPE_VIDEO, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(video))
            {
                result.Add(new BackgroundEntry(BackgroundKind.Official, image, video, "官方动态背景", theme));
            }
            else if (!string.IsNullOrWhiteSpace(image))
            {
                result.Add(new BackgroundEntry(BackgroundKind.Official, image, null, "官方背景", theme));
            }
        }

        return result;
    }


    /// <summary>
    /// 合并：官方视频/图片 → 版本海报（去重）→ 本地自定义（官方在前，本地在后）。
    /// </summary>
    public static IReadOnlyList<BackgroundEntry> Merge(
        IReadOnlyList<BackgroundEntry> official,
        string? posterUrl,
        IReadOnlyList<BackgroundEntry> custom)
    {
        var result = new List<BackgroundEntry>(official.Count + custom.Count + 1);
        result.AddRange(official);

        if (!string.IsNullOrWhiteSpace(posterUrl)
            && !result.Any(x => string.Equals(x.ImageSource, posterUrl, StringComparison.Ordinal)))
        {
            result.Add(new BackgroundEntry(BackgroundKind.Official, posterUrl, null, "官方背景"));
        }

        result.AddRange(custom);
        return result;
    }


    /// <summary>本地背景目录扫描结果（视频/图片按扩展名）。</summary>
    public static IReadOnlyList<BackgroundEntry> FromFiles(IEnumerable<string> files)
    {
        var result = new List<BackgroundEntry>();
        foreach (var file in files)
        {
            var title = Path.GetFileNameWithoutExtension(file);
            if (IsSupportedVideo(file))
            {
                result.Add(new BackgroundEntry(BackgroundKind.Custom, null, file, title));
            }
            else
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp")
                {
                    result.Add(new BackgroundEntry(BackgroundKind.Custom, file, null, title));
                }
            }
        }

        return result;
    }
}
