using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LunarWard.App.ViewModels.Starward;

namespace LunarWard.App.Views.Starward.Controls;

/// <summary>
/// 横向百分比色条（Starward <c>sc:ColorRectChart</c> 的等价物）。
/// </summary>
/// <remarks>
/// Starward 的这个控件画一条高度固定、按 <c>Percent</c> 分段的圆角色条；
/// Avalonia 侧没有现成控件，直接用 <see cref="Visual.Render"/> 画。
/// </remarks>
public sealed class ColorRectChart : Control
{
    /// <summary>分段列表；宽度按 <see cref="IncomeSeriesItem.Percent"/> 占比分配。</summary>
    public static readonly StyledProperty<IReadOnlyList<IncomeSeriesItem>?> SeriesProperty =
        AvaloniaProperty.Register<ColorRectChart, IReadOnlyList<IncomeSeriesItem>?>(nameof(Series));

    public IReadOnlyList<IncomeSeriesItem>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    static ColorRectChart()
    {
        AffectsRender<ColorRectChart>(SeriesProperty);
    }


    public override void Render(DrawingContext context)
    {
        var series = Series;
        if (series is not { Count: > 0 } || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var total = 0;
        foreach (var item in series)
        {
            total += Math.Max(item.Percent, 0);
        }

        if (total <= 0)
        {
            return;
        }

        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        using var clip = context.PushClip(new RoundedRect(bounds, 4));

        var x = 0d;
        foreach (var item in series)
        {
            if (item.Percent <= 0)
            {
                continue;
            }

            var width = bounds.Width * item.Percent / total;
            context.FillRectangle(item.Brush, new Rect(x, 0, width, bounds.Height));
            x += width;
        }
    }
}
