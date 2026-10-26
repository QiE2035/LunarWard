using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Globalization;

namespace LunarWard.Controls;

/// <summary>柱状图数据项（对应 Starward <c>BarChartItem</c>）。</summary>
/// <param name="Value">数值（分钟），决定柱子高度。</param>
/// <param name="Label">横轴标签（日期）。</param>
/// <param name="Tooltip">悬停提示；为空时不显示。</param>
public sealed record PlayTimeBarItem(double Value, string Label, string? Tooltip = null);


/// <summary>
/// 通用柱状图，1:1 移植 Starward <c>Features/PlayTime/Charts/BarChart</c>：
/// 纵轴 3 等分刻度 + 网格线（0 / 中间 / 最大值），纵轴上限按 60 分钟向上取整（最少 60），
/// 圆角柱体、柱宽最大 38、柱间距 10，悬停显示提示。
/// </summary>
public sealed class PlayTimeBarChart : Grid
{
    private static readonly IBrush Secondary = new SolidColorBrush(Color.Parse("#C5FFFFFF"));
    private static readonly IBrush GridLine = new SolidColorBrush(Color.Parse("#1FFFFFFF"));
    private static readonly IBrush BarBrush = new SolidColorBrush(Color.Parse("#CBAD8E"));

    private IReadOnlyList<PlayTimeBarItem> _items = [];

    /// <summary>绘图区高度（像素）。</summary>
    public double PlotHeight { get; set; } = 180;

    /// <summary>柱子最大宽度（像素）。</summary>
    public double MaxBarWidth { get; set; } = 38;


    /// <summary>设置数据并重建图表；空集合时清空。</summary>
    public void SetItems(IReadOnlyList<PlayTimeBarItem> items)
    {
        _items = items ?? [];
        Rebuild();
    }


    private void Rebuild()
    {
        Children.Clear();
        RowDefinitions.Clear();
        ColumnDefinitions.Clear();

        if (_items.Count == 0)
        {
            return;
        }

        double max = _items.Max(x => x.Value);
        double axisMax = Math.Max(60, Math.Ceiling(max / 60.0) * 60.0);

        RowDefinitions.Add(new RowDefinition(new GridLength(PlotHeight)));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(new GridLength(40)));
        ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        // 纵轴刻度：最大值贴顶（文字落在轴下方）、中间值居中、0 贴底
        var axis = new Canvas { Width = 40, Height = PlotHeight };
        for (int i = 0; i < 3; i++)
        {
            double f = i / 2.0;
            var label = new TextBlock
            {
                Text = FormatHours(axisMax * f),
                FontSize = 11,
                Foreground = Secondary,
                TextAlignment = TextAlignment.Right,
                Width = 38,
                Margin = new Thickness(0, 0, 4, 0),
            };
            double top = f switch
            {
                0 => PlotHeight - 16,         // 0h：贴底
                0.5 => PlotHeight / 2 - 8,    // 中间值居中
                _ => 0,                       // 轴上限贴顶
            };
            Canvas.SetTop(label, top);
            Canvas.SetLeft(label, 0);
            axis.Children.Add(label);
        }

        // 网格线铺满绘图区宽度：用 Grid 行保证宽度跟着列走
        var lineLayer = new Grid();
        for (int i = 0; i < 3; i++)
        {
            double f = i / 2.0;
            lineLayer.Children.Add(new Border
            {
                Height = 1,
                Background = GridLine,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, Math.Max(0, PlotHeight * (1 - f)), 0, 0),
                IsHitTestVisible = false,
            });
        }

        Grid.SetRow(axis, 0);
        Grid.SetColumn(axis, 0);
        Children.Add(axis);
        Grid.SetRow(lineLayer, 0);
        Grid.SetColumn(lineLayer, 1);
        Children.Add(lineLayer);

        // 柱体：每项一列，柱高 = 值 / 上限 × 绘图区高度
        var bars = new Grid();
        var labels = new Grid();
        for (int i = 0; i < _items.Count; i++)
        {
            bars.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            labels.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

            PlayTimeBarItem item = _items[i];
            double height = Math.Clamp(item.Value / axisMax * PlotHeight, 0, PlotHeight);

            var bar = new Border
            {
                Height = height,
                MaxWidth = MaxBarWidth,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = BarBrush,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(5, 0, 5, 0),
            };
            if (!string.IsNullOrEmpty(item.Tooltip))
            {
                ToolTip.SetTip(bar, item.Tooltip);
            }

            Grid.SetColumn(bar, i);
            bars.Children.Add(bar);

            var label = new TextBlock
            {
                Text = item.Label,
                FontSize = 10,
                Foreground = Secondary,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
            };
            Grid.SetColumn(label, i);
            labels.Children.Add(label);
        }

        Grid.SetRow(bars, 0);
        Grid.SetColumn(bars, 1);
        Children.Add(bars);

        Grid.SetRow(labels, 1);
        Grid.SetColumn(labels, 1);
        Children.Add(labels);
    }


    /// <summary>分钟 → "0.#h"（Starward 同款）。</summary>
    private static string FormatHours(double minutes)
        => (minutes / 60.0).ToString("0.#", CultureInfo.InvariantCulture) + "h";
}
