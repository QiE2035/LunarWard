using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Globalization;

namespace LunarWard.Controls;

/// <summary>热力图数据项（对应 Starward <c>HeatmapDayItem</c>）。</summary>
/// <param name="Date">日期（本地）。</param>
/// <param name="Value">数值（分钟）；负数视为占位格。</param>
/// <param name="Tooltip">悬停提示。</param>
public sealed record PlayTimeHeatmapDay(DateOnly Date, double Value, string? Tooltip = null);


/// <summary>
/// 日历热力图，移植 Starward <c>Features/PlayTime/Charts/CalendarHeatmap</c>：
/// 列 = 一周（周一起始，7 行填满换列），左侧星期标签列、底部月份标签列，
/// 方块按可用宽度自动算尺寸（默认间距 2），值越大颜色越深（强调色 5 级）。
/// </summary>
public sealed class PlayTimeCalendarHeatmap : Grid
{
    private const double WeekdayLabelWidth = 28;
    private const double MonthLayerHeight = 18;
    private const double Gap = 2;
    private const double MinCell = 6;

    private static readonly IBrush Secondary = new SolidColorBrush(Color.Parse("#C5FFFFFF"));
    private static readonly Color Accent = Color.Parse("#CBAD8E");

    private readonly Canvas _plate = new();
    private readonly Canvas _monthLayer = new();
    private readonly Grid _weekdayColumn = new();
    private IReadOnlyList<PlayTimeHeatmapDay> _days = [];
    private double _lastWidth = -1;


    public PlayTimeCalendarHeatmap()
    {
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(new GridLength(MonthLayerHeight)));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        Grid.SetRow(_monthLayer, 1);
        Grid.SetColumn(_monthLayer, 1);
        Children.Add(_monthLayer);

        Grid.SetRow(_weekdayColumn, 0);
        Grid.SetColumn(_weekdayColumn, 0);
        Children.Add(_weekdayColumn);

        Grid.SetRow(_plate, 0);
        Grid.SetColumn(_plate, 1);
        Children.Add(_plate);

        SizeChanged += (_, _) =>
        {
            double width = Bounds.Width;
            if (_days.Count > 0 && Math.Abs(width - _lastWidth) > 0.5)
            {
                Rebuild();
            }
        };
    }


    /// <summary>设置数据并重建；控件尚未布局时在首次 SizeChanged 里补建。</summary>
    public void SetDays(IReadOnlyList<PlayTimeHeatmapDay> days)
    {
        _days = days ?? [];
        Rebuild();
    }


    private void Rebuild()
    {
        _plate.Children.Clear();
        _monthLayer.Children.Clear();
        _weekdayColumn.Children.Clear();
        _weekdayColumn.RowDefinitions.Clear();

        if (_days.Count == 0)
        {
            return;
        }

        _lastWidth = Bounds.Width;
        double available = Math.Max(120, Bounds.Width - WeekdayLabelWidth - 2);
        int pad = ((int)_days[0].Date.DayOfWeek + 6) % 7;
        int columns = (int)Math.Ceiling((pad + _days.Count) / 7.0);
        // 方块按可用宽度均分（Starward 的做法），单元封顶 16px，保证整年图放得下。
        double pitch = Math.Max(MinCell + Gap, Math.Min(available / Math.Max(1, columns), 18));
        double cell = Math.Clamp(pitch - Gap, MinCell, 16);

        // 左侧星期标签：只显示周一与周日，行高对齐方块
        for (int row = 0; row < 7; row++)
        {
            _weekdayColumn.RowDefinitions.Add(new RowDefinition(new GridLength(pitch)));
            if (row is > 0 and < 6)
            {
                continue;
            }

            DateTime sample = new(2024, 1, 1); // 周一
            var label = new TextBlock
            {
                Text = sample.AddDays(row).ToString("ddd", CultureInfo.CurrentUICulture),
                FontSize = 10,
                Foreground = Secondary,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Width = WeekdayLabelWidth - 6,
                Margin = new Thickness(0, 0, 6, 0),
            };
            Grid.SetRow(label, row);
            _weekdayColumn.Children.Add(label);
        }

        double scaleMax = Math.Max(1, _days.Max(x => x.Value));

        for (int i = 0; i < pad; i++)
        {
            AddSlot(i, cell, pitch, transparent: true, null);
        }

        int month = -1;
        for (int index = 0; index < _days.Count; index++)
        {
            PlayTimeHeatmapDay day = _days[index];
            int slot = pad + index;
            int dayMonth = day.Date.Year * 12 + day.Date.Month;
            if (dayMonth != month)
            {
                month = dayMonth;
                if (day.Date.Day == 1)
                {
                    var label = new TextBlock
                    {
                        Text = day.Date.ToDateTime(TimeOnly.MinValue).ToString("MMM", CultureInfo.CurrentUICulture),
                        FontSize = 11,
                        Foreground = Secondary,
                    };
                    Canvas.SetLeft(label, slot / 7 * pitch);
                    Canvas.SetTop(label, 2);
                    _monthLayer.Children.Add(label);
                }
            }

            AddSlot(slot, cell, pitch, transparent: false, day, scaleMax);
        }
    }


    private void AddSlot(int slot, double cell, double pitch, bool transparent)
        => AddSlot(slot, cell, pitch, transparent, null, 0);


    private void AddSlot(int slot, double cell, double pitch, bool transparent, PlayTimeHeatmapDay? day, double scaleMax = 0)
    {
        var border = new Border
        {
            Width = cell,
            Height = cell,
            CornerRadius = new CornerRadius(2),
            Background = transparent || day is null ? Brushes.Transparent : BrushFor(day.Value, scaleMax),
        };
        if (day?.Tooltip is { Length: > 0 } tooltip)
        {
            ToolTip.SetTip(border, tooltip);
        }

        Canvas.SetLeft(border, slot / 7 * pitch);
        Canvas.SetTop(border, slot % 7 * pitch);
        _plate.Children.Add(border);
    }


    /// <summary>值 → 强调色 5 级（0 值为很淡的底色，与空格区分）。</summary>
    private static IBrush BrushFor(double value, double scaleMax)
    {
        if (value <= 0)
        {
            return new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
        }

        int level = Math.Clamp((int)Math.Ceiling(value / scaleMax * 5), 1, 5);
        byte alpha = level switch
        {
            1 => 0x40,
            2 => 0x66,
            3 => 0x8C,
            4 => 0xB4,
            _ => 0xE0,
        };
        return new SolidColorBrush(Color.FromArgb(alpha, Accent.R, Accent.G, Accent.B));
    }
}
