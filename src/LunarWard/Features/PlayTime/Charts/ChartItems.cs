using System;

namespace LunarWard.Features.PlayTime.Charts;

/// <summary>
/// 柱状图数据项，对应上游 <c>Starward.Features.PlayTime.BarChartItem</c>。
/// </summary>
public class BarChartItem
{

    /// <summary>
    /// 数值，决定柱子高度
    /// </summary>
    public double Value { get; set; }


    /// <summary>
    /// 横轴标签（如日期）
    /// </summary>
    public string Label { get; set; } = "";


    /// <summary>
    /// 悬停提示文本，为空时不显示悬浮卡
    /// </summary>
    public string? Tooltip { get; set; }

}



/// <summary>
/// 日历热力图数据项，对应上游 <c>Starward.Features.PlayTime.HeatmapDayItem</c>。
/// </summary>
public class HeatmapDayItem
{

    /// <summary>
    /// 日期（本地时间）
    /// </summary>
    public DateOnly Date { get; set; }


    /// <summary>
    /// 数值，负数视为占位格（透明）
    /// </summary>
    public double Value { get; set; }


    /// <summary>
    /// 悬停提示文本，为空时不显示悬浮卡
    /// </summary>
    public string? Tooltip { get; set; }

}
