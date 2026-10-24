using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LunarWard.App.Controls;

/// <summary>
/// 一条图例（Starward <c>ColorRectChart.ChartLegend</c>）：<see cref="Legend"/> 为图例文字，
/// <see cref="Percent"/> 为占比，<see cref="Color"/> 为该段颜色。
/// </summary>
public readonly record struct ChartLegend(string? Legend, int Percent, IBrush Color);


/// <summary>
/// 彩色分段条（Starward <c>Controls/ColorRectChart.xaml</c> 的 Avalonia 移植）。
/// </summary>
/// <remarks>
/// 用 <see cref="LinearGradientBrush"/> 的硬边渐变（同一 offset 两个 stop）拼出每段的色块，
/// 不依赖 WinUI 的 Composition。空数据或全 0 时显示 <c>#60000000</c> 底。
/// </remarks>
public sealed class ColorRectChart : Border
{
    public static readonly StyledProperty<IReadOnlyList<ChartLegend>?> SeriesProperty =
        AvaloniaProperty.Register<ColorRectChart, IReadOnlyList<ChartLegend>?>(nameof(Series));

    private static readonly IBrush EmptyBrush = new SolidColorBrush(Color.FromArgb(0x60, 0, 0, 0));


    static ColorRectChart()
    {
        AffectsRender<ColorRectChart>(SeriesProperty);
        SeriesProperty.Changed.AddClassHandler<ColorRectChart>((chart, _) => chart.ApplySeries());
    }


    public ColorRectChart()
    {
        CornerRadius = new CornerRadius(4);
        Background = EmptyBrush;
    }


    public IReadOnlyList<ChartLegend>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }


    private void ApplySeries()
    {
        var series = Series;
        if (series is null || series.Count == 0 || series.Sum(x => x.Percent) <= 0)
        {
            Background = EmptyBrush;
            return;
        }

        double sum = series.Sum(x => x.Percent);
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };

        double offset = 0;
        foreach (var item in series)
        {
            var color = item.Color is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
            brush.GradientStops.Add(new GradientStop(color, offset));
            offset += item.Percent / sum;
            brush.GradientStops.Add(new GradientStop(color, offset));
        }

        Background = brush;
    }
}
