namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 截图月份胶囊的纯计算部分（不碰 UI，便于单测）。
/// </summary>
/// <remarks>
/// 上游用 WinUI 的 <c>AnnotatedScrollBar</c> 在滚动条上标注月份分组；Avalonia 没有这个控件，
/// LunarWard 用一个跟随滚动的胶囊近似：滚动时取「视口顶端」落在哪个月份分组。
/// 分组依据是磁贴墙里每个磁贴的 Y 坐标与它的截图时间（见 <see cref="ScreenshotItemViewModel"/>）。
/// </remarks>
public static class ScreenshotMonthIndex
{
    /// <summary>把截图时间格式化成胶囊文案（<c>yyyy-MM</c>，与上游 AnnotatedScrollBar 的分组标题一致）。</summary>
    public static string Label(DateTimeOffset time) => $"{time.Year}-{time.Month:00}";

    /// <summary>
    /// 在「磁贴顶边 Y → 月份」映射里查找 <paramref name="offsetY"/> 所在的分组。
    /// </summary>
    /// <param name="map">按 Y 升序排列的分组起点；同一月份可能有多项，取最后一项。</param>
    /// <param name="offsetY">视口顶端的内容坐标。</param>
    /// <returns>命中的月份文案；映射为空时返回 <see langword="null"/>。</returns>
    public static string? Locate(IReadOnlyList<(double Y, string Month)> map, double offsetY)
    {
        if (map.Count == 0)
        {
            return null;
        }

        int lo = 0, hi = map.Count - 1, found = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (map[mid].Y <= offsetY)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return map[found].Month;
    }
}
