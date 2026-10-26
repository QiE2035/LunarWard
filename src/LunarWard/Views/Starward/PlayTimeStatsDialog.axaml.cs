using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using LunarWard.Controls;
using LunarWard.Localization;
using LunarWard.Services;
using LunarWard.Views.Starward.Controls;
using LunarWard.Launch;
using System.Globalization;

namespace LunarWard.Views.Starward;

/// <summary>
/// 游戏时长统计弹窗，1:1 移植 Starward <c>Features/PlayTime/PlayTimeStatsDialog</c>：
/// 6 张统计卡片（总时长/每日平均/最长连续/最长单次/单日最长/上次启动）、
/// 15 天 / 12 周 / 12 月可切换柱状图、最近一年日历热力图。
/// </summary>
/// <remarks>
/// 数据只来自本机 SQLite 库的 <c>PlayTimeStats</c> 表（<see cref="PlayTimeService" />），
/// 派生口径与 Starward 相同（见 <see cref="PlayTimeStats" />）。
/// </remarks>
public partial class PlayTimeStatsDialog : StarwardContentDialog
{
    private static AppStrings Strings => AppStrings.Instance;

    private readonly PlayTimeStats _stats;
    private int _range;


    /// <summary>设计器专用。</summary>
    public PlayTimeStatsDialog()
        : this(null!, "")
    {
    }


    public PlayTimeStatsDialog(AppServices services, string gameBiz)
    {
        InitializeComponent();

        _stats = services is null
            ? PlayTimeStats.FromSpans([])
            : PlayTimeStats.FromSessions(services.PlayTime.Sessions(gameBiz), gameBiz);

        CloseButtonText = Strings["Common.Close"];

        BuildStatCards();
        if (int.TryParse(Environment.GetEnvironmentVariable("LUNARWARD_PLAYTIME_RANGE"), out int range)
            && range is >= 0 and <= 2)
        {
            _range = range;
        }

        UpdateRange();
    }


    private void BuildStatCards()
    {
        string total = TimeSpanToString(TimeSpan.FromMilliseconds(_stats.TotalMilliseconds));
        string average = _stats.PlayDays > 0
            ? TimeSpanToString(TimeSpan.FromMilliseconds(_stats.AverageDayMilliseconds))
            : "-";
        string longestSession = TimeSpanToString(TimeSpan.FromMilliseconds(_stats.LongestSpanMilliseconds));
        string maxDay = _stats.MaxDayMilliseconds > 0
            ? TimeSpanToString(TimeSpan.FromMilliseconds(_stats.MaxDayMilliseconds))
            : "-";
        string lastDuration = _stats.LastPlayStart is not null
            ? TimeSpanToString(TimeSpan.FromMilliseconds(_stats.LastPlayMilliseconds))
            : "-";

        (string Title, string Value, string Sub)[] cards =
        [
            (Strings["PlayTime.Total"], total,
                _stats.TotalMilliseconds > 0
                    ? string.Format(Strings["PlayTime.StartedTimes"], _stats.Sessions.Count)
                    : ""),
            (Strings["PlayTime.AverageDaily"], average,
                _stats.PlayDays > 0 ? string.Format(Strings["PlayTime.PlayedDays"], _stats.PlayDays) : ""),
            (Strings["PlayTime.LongestStreak"], string.Format(Strings["PlayTime.Days"], _stats.LongestStreakDays),
                _stats.LongestStreakText),
            (Strings["PlayTime.LongestSession"], longestSession,
                _stats.LongestSpanStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""),
            (Strings["PlayTime.LongestDaily"], maxDay,
                _stats.MaxDayDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""),
            (Strings["PlayTime.LastStartup"], lastDuration,
                _stats.LastPlayStart?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? ""),
        ];

        StatCardsGrid.ColumnDefinitions.Clear();
        StatCardsGrid.RowDefinitions.Clear();
        StatCardsGrid.Children.Clear();
        for (int column = 0; column < 3; column++)
        {
            StatCardsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        }

        for (int row = 0; row < 2; row++)
        {
            StatCardsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (int i = 0; i < cards.Length; i++)
        {
            (string title, string value, string sub) = cards[i];
            var panel = new StackPanel { Spacing = 2 };
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                Foreground = Brush("#C5FFFFFF"),
            });
            panel.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush("#FFFFFFFF"),
            });
            if (sub.Length > 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = sub,
                    FontSize = 12,
                    Foreground = Brush("#87FFFFFF"),
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            var card = new Border
            {
                Background = Brush("#0FFFFFFF"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8),
                Child = panel,
            };
            Grid.SetRow(card, i / 3);
            Grid.SetColumn(card, i % 3);
            StatCardsGrid.Children.Add(card);
        }

        EmptyText.IsVisible = _stats.Sessions.Count == 0;
    }


    private void OnRangeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out int range) && _range != range)
        {
            _range = range;
            UpdateRange();
        }
    }


    private void UpdateRange()
    {
        foreach ((Button button, int value) in new[]
                 {
                     (RangeDaysButton, 0), (RangeWeeksButton, 1), (RangeMonthsButton, 2),
                 })
        {
            button.Classes.Set("selected", value == _range);
        }

        var (items, total) = BuildBarItems(_range);
        BarChart.SetItems(items);
        RangeTotalText.Text = string.Format(
            Strings["PlayTime.TotalInRange"],
            TimeSpanToString(TimeSpan.FromMilliseconds(total)));

        Heatmap.SetDays(BuildHeatmap());
    }


    private (IReadOnlyList<PlayTimeBarItem> Items, long Total) BuildBarItems(int range)
    {
        DateTime today = DateTime.Today;
        var items = new List<PlayTimeBarItem>();
        long total = 0;

        if (range == 1)
        {
            // 最近 12 个自然周（周一至周日），以今天所在周为最后一周
            int sinceMonday = ((int)today.DayOfWeek + 6) % 7;
            DateTime firstMonday = today.AddDays(-sinceMonday - 11 * 7);
            for (int week = 0; week < 12; week++)
            {
                DateTime start = firstMonday.AddDays(week * 7);
                DateTime end = start.AddDays(6);
                DateTime actualLast = end <= today ? end : today;
                long sum = SumDays(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end));
                total += sum;
                items.Add(new PlayTimeBarItem(
                    sum / 60_000.0,
                    start.ToString("MM-dd", CultureInfo.InvariantCulture),
                    $"{start:MM/dd} - {actualLast:MM/dd}\n{TimeSpanToString(TimeSpan.FromMilliseconds(sum))}"));
            }
        }
        else if (range == 2)
        {
            // 最近 12 个自然月，从上月月初往前推
            DateTime firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
            for (int month = 0; month < 12; month++)
            {
                DateTime start = firstMonth.AddMonths(month);
                DateTime end = start.AddMonths(1).AddDays(-1);
                long sum = SumDays(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end));
                total += sum;
                items.Add(new PlayTimeBarItem(
                    sum / 60_000.0,
                    start.ToString("MMM", CultureInfo.CurrentUICulture),
                    $"{start:yyyy-MM}\n{TimeSpanToString(TimeSpan.FromMilliseconds(sum))}"));
            }
        }
        else
        {
            DateTime firstDay = today.AddDays(-14);
            for (int day = 0; day < 15; day++)
            {
                DateTime date = firstDay.AddDays(day);
                long ms = _stats.MillisecondsOn(DateOnly.FromDateTime(date));
                total += ms;
                items.Add(new PlayTimeBarItem(
                    Math.Max(0, ms / 60_000.0),
                    date.ToString("MM-dd", CultureInfo.InvariantCulture),
                    $"{date:yyyy-MM-dd}\n{TimeSpanToString(TimeSpan.FromMilliseconds(ms))}"));
            }
        }

        return (items, total);
    }


    private IReadOnlyList<PlayTimeHeatmapDay> BuildHeatmap()
    {
        // 最近一年 52 个自然周，首日一定是周一
        DateTime today = DateTime.Today;
        int sinceMonday = ((int)today.DayOfWeek + 6) % 7;
        DateTime firstDay = today.AddDays(-sinceMonday - 51 * 7);
        int days = (today - firstDay).Days + 1;
        var items = new List<PlayTimeHeatmapDay>(days);
        for (int i = 0; i < days; i++)
        {
            DateTime date = firstDay.AddDays(i);
            long ms = _stats.MillisecondsOn(DateOnly.FromDateTime(date));
            items.Add(new PlayTimeHeatmapDay(
                DateOnly.FromDateTime(date),
                Math.Max(0, ms / 60_000.0),
                $"{date:yyyy-MM-dd}\n{TimeSpanToString(TimeSpan.FromMilliseconds(ms))}"));
        }

        return items;
    }


    private long SumDays(DateOnly start, DateOnly end)
    {
        long sum = 0;
        for (DateOnly day = start; day <= end; day = day.AddDays(1))
        {
            sum += _stats.MillisecondsOn(day);
        }

        return sum;
    }


    /// <summary>毫秒 → "0m" / "30m" / "2h" / "2h 30m"（Starward <c>TimeSpanToString</c>）。</summary>
    internal static string TimeSpanToString(TimeSpan span)
    {
        int totalMinutes = (int)Math.Round(span.TotalMinutes);
        if (totalMinutes < 1)
        {
            return "0m";
        }

        if (totalMinutes < 60)
        {
            return $"{totalMinutes}m";
        }

        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
    }


    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
