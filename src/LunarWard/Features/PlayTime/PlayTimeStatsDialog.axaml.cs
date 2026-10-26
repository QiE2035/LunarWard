using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.Database;
using LunarWard.Features.PlayTime.Charts;
using LunarWard.Frameworks.Dialogs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LunarWard.Features.PlayTime;

/// <summary>
/// 游戏时长统计对话框：统计卡片 + 柱状图（15 天 / 12 周 / 12 月）+ 一年日历热力图。
/// </summary>
/// <remarks>
/// 对应上游 <c>Starward.Features.PlayTime.PlayTimeStatsDialog</c>。平台差异（docs/09 十六）：
/// <c>ContentDialog.ShowAsync()</c>→<see cref="Services.DialogService.ShowAsync"/>；
/// <c>this.Hide()</c>→<see cref="StarwardContentDialog.Close"/>；
/// <c>CommunityToolkit Segmented</c>→3 个 <see cref="ToggleButton"/> + <see cref="BarRange_Checked"/> 互斥；
/// <c>ItemsRepeater + UniformGridLayout</c>→<c>ItemsControl + UniformGrid</c>；
/// <c>[INotifyPropertyChanged]</c>→手写 <c>SetProperty</c>（Avalonia 控件已实现 INPC）；
/// <c>UniformGridLayout.MinItemHeight</c> 无对应物，长文案分支只调间距（已在 docs/09 登记）。
/// </remarks>
public sealed partial class PlayTimeStatsDialog : StarwardContentDialog, INotifyPropertyChanged
{


    private readonly ILogger<PlayTimeStatsDialog> _logger = AppConfig.GetLogger<PlayTimeStatsDialog>();

    private readonly PlayTimeStatsService _playTimeStatsService = AppConfig.GetService<PlayTimeStatsService>();

    private Dictionary<DateOnly, long> _playTimePerDay = [];



    public PlayTimeStatsDialog()
    {
        InitializeComponent();
        DataContext = this;
        this.Loaded += PlayTimeStatsDialog_Loaded;
        this.Unloaded += PlayTimeStatsDialog_Unloaded;
    }



    /// <summary>当前游戏 ID（上游保留字段；统计只用到 <see cref="CurrentGameBiz"/>）。</summary>
    public GameId CurrentGameId { get; set; }


    /// <summary>当前游戏。</summary>
    public GameBiz CurrentGameBiz { get; set; }




    private void PlayTimeStatsDialog_Loaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            _playTimeStatsService.ConvertItemToStats();
            LoadPlayTimeStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load play time stats: GameBiz {biz}", CurrentGameBiz);
        }
        _playTimeLoaded = true;
    }


    private void PlayTimeStatsDialog_Unloaded(object? sender, RoutedEventArgs e)
    {
        this.Loaded -= PlayTimeStatsDialog_Loaded;
        this.Unloaded -= PlayTimeStatsDialog_Unloaded;
        StatCards = null;
        PlayTimeBarChart.Items = null;
        PlayTimeHeatmap.Days = null;
        _playTimeLoaded = false;
    }


    private bool _playTimeLoaded;


    /// <summary>总时长文本</summary>
    public string TotalTimeText { get; set => SetProperty(ref field, value); } = "";

    /// <summary>启动次数</summary>
    public string StartUpCountText { get; set => SetProperty(ref field, value); } = "";

    /// <summary>每日平均游戏时间</summary>
    public string AverageDayTimeText { get; set => SetProperty(ref field, value); } = "";

    /// <summary>游玩天数</summary>
    public string PlayDaysText { get; set => SetProperty(ref field, value); } = "";

    /// <summary>最长连续游玩天数</summary>
    public int LongestContinuousDays { get; set => SetProperty(ref field, value); }

    /// <summary>最长连续游玩天数文本</summary>
    public string LongestContinuousDaysText { get; set => SetProperty(ref field, value); } = "";

    /// <summary>最长单次游玩时长文本</summary>
    public string LongestRunTimeText { get; set => SetProperty(ref field, value); } = "";

    /// <summary>最长单次游玩起始日期文本</summary>
    public string LongestRunStartText { get; set => SetProperty(ref field, value); } = "";

    /// <summary>上一次游玩时长文本</summary>
    public string LastPlayDurationText { get; set => SetProperty(ref field, value); } = "";

    /// <summary>上一次游玩起始时间文本</summary>
    public string LastPlayTimeText { get; set => SetProperty(ref field, value); } = "";

    /// <summary>单日最长游玩时长文本</summary>
    public string MaxDayPlayTimeText { get; set => SetProperty(ref field, value); } = "";


    /// <summary>单日最长游玩起始日期文本</summary>
    public string MaxDayPlayDateText { get; set => SetProperty(ref field, value); } = "";


    /// <summary>柱状图当前区间总时长文本</summary>
    public string BarTotalText { get; set => SetProperty(ref field, value); } = "";


    /// <summary>统计卡片数据项</summary>
    public List<StatCardItem>? StatCards { get; set => SetProperty(ref field, value); }



    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }



    /// <summary>
    /// 加载全部统计：单次查询会话区间，内存中派生所有属性与图表数据
    /// </summary>
    private void LoadPlayTimeStats()
    {
        var biz = CurrentGameBiz;
        try
        {
            var sessions = _playTimeStatsService.GetPlayTimeInRange(biz, default, DateTimeOffset.Now);
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // 总时长
            long totalMs = 0;
            // 最长单次游玩
            long longestSpan = 0, longestStart = 0;
            // 上一次游玩
            long lastStart = 0, lastSpan = 0;
            // 每日时长
            Dictionary<DateOnly, long> timePerDay = new Dictionary<DateOnly, long>();

            foreach (var session in sessions)
            {
                long span = session.EndTime - session.StartTime;
                totalMs += span;
                if (span > longestSpan)
                {
                    longestSpan = span;
                    longestStart = session.StartTime;
                }

                // 游戏关闭后60s才会被认为是上一次启动
                if (session.StartTime > lastStart && now - session.EndTime > 60_000)
                {
                    lastStart = session.StartTime;
                    lastSpan = span;
                }

                DateTimeOffset startTime = DateTimeOffset.FromUnixTimeMilliseconds(session.StartTime).ToLocalTime();
                DateTimeOffset endTime = DateTimeOffset.FromUnixTimeMilliseconds(session.EndTime).ToLocalTime();

                // 计算每日时长，把数据添加到 timePerDay 字典中
                for (DateTime day = startTime.Date; day <= endTime.Date; day = day.AddDays(1))
                {
                    DateTimeOffset dayStart = day == startTime.Date ? startTime : day;
                    DateTimeOffset dayEnd = day == endTime.Date ? endTime : day.AddDays(1).AddTicks(-1);
                    var duration = (long)(dayEnd - dayStart).TotalMilliseconds;
                    var dateOnly = DateOnly.FromDateTime(day);
                    if (timePerDay.TryGetValue(dateOnly, out long v))
                    {
                        timePerDay[dateOnly] = v + duration;
                    }
                    else
                    {
                        timePerDay[dateOnly] = duration;
                    }
                }
            }

            _playTimePerDay = timePerDay;

            TotalTimeText = TimeSpanToString(TimeSpan.FromMilliseconds(totalMs));
            StartUpCountText = totalMs > 0 ? string.Format(Lang.PlayTimeStatsDialog_Started0Times, sessions.Count) : "";
            DatabaseService.SetValue($"playtime_total_{biz}", TimeSpan.FromMilliseconds(totalMs));

            AverageDayTimeText = timePerDay.Count > 0 ? TimeSpanToString(TimeSpan.FromMilliseconds(totalMs / timePerDay.Count)) : "-";
            PlayDaysText = timePerDay.Count > 0 ? string.Format(Lang.PlayTimeStatsDialog_PlayedFor0Days, timePerDay.Count) : "";

            // 最长连续游玩天数和起止日期
            int longestContinuousDays = 0;
            DateOnly? longestContinuousStart = null;
            DateOnly? longestContinuousEnd = null;
            var orderedDays = timePerDay.Keys.OrderBy(d => d).ToList();

            DateOnly? currentStart = null;
            DateOnly? previousDay = null;
            int currentStreak = 0;

            foreach (var orderedDay in orderedDays)
            {
                if (previousDay.HasValue && orderedDay == previousDay.Value.AddDays(1))
                {
                    // 与前一天相邻，延长当前连续段
                    currentStreak++;
                }
                else
                {
                    // 与前一天不相邻，开始新的连续段
                    currentStart = orderedDay;
                    currentStreak = 1;
                }

                if (currentStreak > longestContinuousDays)
                {
                    longestContinuousDays = currentStreak;
                    longestContinuousStart = currentStart;
                    longestContinuousEnd = orderedDay;
                }

                previousDay = orderedDay;
            }

            if (longestContinuousDays > 0)
            {
                LongestContinuousDays = longestContinuousDays;
                LongestContinuousDaysText = $"{longestContinuousStart:yyyy/MM/dd} - {longestContinuousEnd:yyyy/MM/dd}";
            }
            else
            {
                LongestContinuousDays = 0;
                LongestContinuousDaysText = "";
            }

            // 单日最长游玩时长和日期
            long maxDayMs = 0;
            DateOnly maxDayDate = default;
            foreach (var (day, ms) in timePerDay)
            {
                if (ms > maxDayMs)
                {
                    maxDayMs = ms;
                    maxDayDate = day;
                }
            }

            if (maxDayMs > 0)
            {
                MaxDayPlayTimeText = TimeSpanToString(TimeSpan.FromMilliseconds(maxDayMs));
                MaxDayPlayDateText = maxDayDate.ToString("yyyy-MM-dd");
            }
            else
            {
                MaxDayPlayTimeText = "-";
                MaxDayPlayDateText = "";
            }

            LongestRunTimeText = TimeSpanToString(TimeSpan.FromMilliseconds(longestSpan));
            LongestRunStartText = longestSpan > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(longestStart).LocalDateTime.ToString("yyyy-MM-dd") : "";
            if (lastStart > 0)
            {
                LastPlayDurationText = TimeSpanToString(TimeSpan.FromMilliseconds(Math.Max(lastSpan, 60_000)));
                LastPlayTimeText = DateTimeOffset.FromUnixTimeMilliseconds(lastStart).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
            }
            else
            {
                LastPlayDurationText = "-";
                LastPlayTimeText = "";
            }

            StatCards =
            [
                new StatCardItem { Title = Lang.PlayTimeStatsDialog_TotalPlaytime, Value = TotalTimeText, SubText = StartUpCountText },
                new StatCardItem { Title = Lang.PlayTimeStatsDialog_AverageDailyPlaytime, Value = AverageDayTimeText, SubText = PlayDaysText },
                new StatCardItem { Title = Lang.PlayTimeStatsDialog_LongestStreak, Value = string.Format(Lang.PlayTimeStatsDialog_0Days, LongestContinuousDays), SubText = LongestContinuousDaysText },
                new StatCardItem { Title = Lang.PlayTimeStatsDialog_LongestSession, Value = LongestRunTimeText, SubText = LongestRunStartText },
                new StatCardItem { Title = Lang.PlayTimeStatsDialog_LongestDailyPlaytime, Value = MaxDayPlayTimeText, SubText = MaxDayPlayDateText },
                new StatCardItem { Title = Lang.PlayTimeButton_LastStartup, Value = LastPlayDurationText, SubText = LastPlayTimeText },
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load play time stats: GameBiz {biz}", biz);
        }

        try
        {
            BuildBarChart();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build bar chart: GameBiz {biz}, range {range}", CurrentGameBiz, BarRangeIndex);
        }

        try
        {
            BuildHeatmap();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build heatmap: GameBiz {biz}", CurrentGameBiz);
        }


        if (Lang.PlayTimeStatsDialog_AverageDailyPlaytime.Length > 22)
        {
            // 上游还在这里抬高 UniformGridLayout_StatCards.MinItemHeight（卡片两行标题变高）；
            // Avalonia 的 UniformGrid 没有该属性，卡片高度由内容自适应，故只保留两处间距调整。
            Grid_BarChartSwitcher.Margin = new Thickness(4, 16, 4, 0);
            PlayTimeHeatmap.Margin = new Thickness(0, 20, 0, 0);
        }

    }



    /// <summary>柱状图区间：0 = 最近 15 天，1 = 最近 12 个自然周，2 = 最近 12 个月（对应上游 Segmented.SelectedIndex）。</summary>
    private int BarRangeIndex
    {
        get
        {
            foreach (var child in Segmented_BarRange?.Children ?? [])
            {
                if (child is ToggleButton { IsChecked: true, Tag: string tag } && int.TryParse(tag, out int index))
                {
                    return index;
                }
            }
            return 0;
        }
    }



    private void BuildBarChart()
    {
        int range = BarRangeIndex;
        var today = DateTime.Today;
        if (range == 1)
        {
            // 最近 12 个自然周（周一 ～ 周日为一周）：以今天所在的周为最后一周，往前共 12 组
            int sinceMonday = ((int)today.DayOfWeek + 6) % 7; // 本周已过天数（0 = 周一）
            var firstMonday = today.AddDays(-sinceMonday - 11 * 7);
            var items = new List<BarChartItem>(12);
            long total = 0;
            for (int w = 0; w < 12; w++)
            {
                var weekStart = firstMonday.AddDays(w * 7);
                var weekEnd = weekStart.AddDays(6);
                var actualLast = weekEnd <= today ? DateOnly.FromDateTime(weekEnd) : DateOnly.FromDateTime(today);
                long sum = SumDayRange(DateOnly.FromDateTime(weekStart), DateOnly.FromDateTime(weekEnd));
                total += sum;
                items.Add(new BarChartItem
                {
                    Label = weekStart.ToString("MM-dd"),
                    Value = sum / 60_000.0,
                    Tooltip = $"{weekStart:MM/dd} - {actualLast:MM/dd}\n{TimeSpanToString(TimeSpan.FromMilliseconds(sum))}",
                });
            }
            PlayTimeBarChart.Items = items;
            BarTotalText = TimeSpanToString(TimeSpan.FromMilliseconds(total));
        }
        else if (range == 2)
        {
            // 最近 12 个月：从上月月初（共 12 个自然月对齐）到今天的每日数据按月聚合
            var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
            var items = new List<BarChartItem>(12);
            long total = 0;
            for (int m = 0; m < 12; m++)
            {
                var monthStart = firstMonth.AddMonths(m);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                long sum = SumDayRange(DateOnly.FromDateTime(monthStart), DateOnly.FromDateTime(monthEnd));
                total += sum;
                items.Add(new BarChartItem
                {
                    Label = monthStart.ToString("MMM", CultureInfo.CurrentUICulture),
                    Value = sum / 60_000.0,
                    Tooltip = $"{monthStart:yyyy-MM}\n{TimeSpanToString(TimeSpan.FromMilliseconds(sum))}",
                });
            }
            PlayTimeBarChart.Items = items;
            BarTotalText = TimeSpanToString(TimeSpan.FromMilliseconds(total));
        }
        else
        {
            var firstDay = today.AddDays(-14);
            var items = new List<BarChartItem>(15);
            long total = 0;
            for (int i = 0; i < 15; i++)
            {
                var d = firstDay.AddDays(i);
                long ms = _playTimePerDay.GetValueOrDefault(DateOnly.FromDateTime(d));
                total += ms;
                items.Add(new BarChartItem
                {
                    Label = d.ToString("MM-dd"),
                    Value = Math.Max(0, ms / 60_000.0),
                    Tooltip = $"{d:yyyy-MM-dd}\n{TimeSpanToString(TimeSpan.FromMilliseconds(ms))}",
                });
            }
            PlayTimeBarChart.Items = items;
            BarTotalText = TimeSpanToString(TimeSpan.FromMilliseconds(total));
        }
    }


    /// <summary>
    /// 对应上游 <c>Segmented_BarRange_SelectionChanged</c>：三个 ToggleButton 互斥（Avalonia 无 Segmented 控件），
    /// 选中项变化后重建柱状图。
    /// </summary>
    private void BarRange_Checked(object? sender, RoutedEventArgs e)
    {
        // Avalonia 的 ToggleButton 只有 IsCheckedChanged；取消其它按钮的选中会再次进入本方法，
        // 因此先挡掉“被取消”的那一次（IsChecked 不是 true 直接返回）。
        if (sender is not ToggleButton { IsChecked: true } checkedButton)
        {
            return;
        }
        // XAML 里的 IsChecked="True" 会在 InitializeComponent 期间触发 Checked，
        // 此时命名字段可能尚未赋值（也可能还没加载完数据），两种都直接忽略。
        if (Segmented_BarRange is not { } panel)
        {
            return;
        }
        foreach (var child in panel.Children)
        {
            if (child is ToggleButton button && !ReferenceEquals(button, checkedButton))
            {
                button.IsChecked = false;
            }
        }
        if (!_playTimeLoaded)
        {
            return;
        }
        try
        {
            BuildBarChart();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rebuild bar chart: GameBiz {biz}", CurrentGameBiz);
        }
    }



    private void BuildHeatmap()
    {
        // 最近一年，52个自然周
        var today = DateTime.Today;
        // 先回溯到本周周一（周一 = 0），再往前 51 周，保证首日一定是周一。
        // 注意不能用 ((int)DayOfWeek - 1) % 6：C# 取余保留被除数符号，周日会得到 -1 而反向偏移一天。
        int sinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var firstDay = today.AddDays(-sinceMonday - 51 * 7);
        int totalDays = (today - firstDay).Days + 1;
        var items = new List<HeatmapDayItem>(totalDays);
        long total = 0;
        for (int i = 0; i < totalDays; i++)
        {
            var d = firstDay.AddDays(i);
            long ms = _playTimePerDay.GetValueOrDefault(DateOnly.FromDateTime(d));
            total += ms;
            items.Add(new HeatmapDayItem
            {
                Date = DateOnly.FromDateTime(d),
                Value = Math.Max(0, ms / 60_000.0),
                Tooltip = $"{d:yyyy-MM-dd}\n{TimeSpanToString(TimeSpan.FromMilliseconds(ms))}",
            });
        }
        PlayTimeHeatmap.Days = items;
    }



    /// <summary>
    /// 累加 [start, end] 日期区间内的每日游戏毫秒数
    /// </summary>
    private long SumDayRange(DateOnly start, DateOnly end)
    {
        long sum = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            sum += _playTimePerDay.GetValueOrDefault(d);
        }
        return sum;
    }



    /// <summary>把时长格式化为「0m / Xm / Xh / Xh Ym」（与上游逐字一致）。</summary>
    public static string TimeSpanToString(TimeSpan timeSpan)
    {
        int totalMinutes = (int)Math.Round(timeSpan.TotalMinutes);
        if (totalMinutes < 1)
        {
            return "0m";
        }
        if (totalMinutes < 60)
        {
            return $"{totalMinutes}m";
        }
        int hours = totalMinutes / 60, minutes = totalMinutes % 60;
        return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
    }



    #region INotifyPropertyChanged

    private PropertyChangedEventHandler? _propertyChanged;

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }


    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        OnPropertyChanged(propertyName);
    }

    #endregion

}
