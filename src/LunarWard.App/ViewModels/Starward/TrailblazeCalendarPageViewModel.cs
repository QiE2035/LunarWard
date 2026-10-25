using LunarWard.App.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.StarRail.TrailblazeCalendar;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 开拓月历页 VM（Starward <c>Features/GameRecord/StarRail/TrailblazeCalendarPage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 结构 = 左栏「刷新 + 获取明细 + 月份列表（每月星琼数）」
/// + 右侧「实时数据卡（今日/当月获取 + 星琼收入组成图 + 图例）→ 历史数据卡（选中月同构）
/// → 每日数据卡（按月内每天画两条进度条：星琼 / 古老梦华）→ 可选明细列表」。
/// </para>
/// <para>
/// Starward 的每日数据来自本地 SQLite 的明细表（需先「获取明细」）；LunarWard 没有数据库，
/// 选中月份时直接调 <c>GetTrailblazeCalendarDetailAsync(role, month, type)</c> 现拉两类明细再按天累加。
/// </para>
/// </remarks>
public sealed partial class TrailblazeCalendarPageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>星琼收入组成的动作配色（源码 <c>actionColorMap</c>）。</summary>
    private static readonly Dictionary<string, Color> ActionColors = new(StringComparer.Ordinal)
    {
        ["daily_reward"] = Color.Parse("#FEC66F"),
        ["space_reward"] = Color.Parse("#44DD9C"),
        ["event_reward"] = Color.Parse("#47C6FD"),
        ["adventure_reward"] = Color.Parse("#887FFE"),
        ["abyss_reward"] = Color.Parse("#DF53FE"),
        ["mail_reward"] = Color.Parse("#F84E35"),
        ["other"] = Color.Parse("#FDEA60"),
    };

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    private readonly Dictionary<string, TrailblazeCalendarSummary> _summaries = new(StringComparer.Ordinal);

    private bool _initialized;

    private bool _applyingSelection;

    private CancellationTokenSource? _detailCts;


    public TrailblazeCalendarPageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _ = InitializeAsync();
    }


    public ObservableCollection<TrailblazeMonthItem> MonthList { get; } = [];

    [ObservableProperty]
    private TrailblazeMonthItem? _selectedMonth;

    partial void OnSelectedMonthChanged(TrailblazeMonthItem? value)
    {
        if (_applyingSelection)
        {
            return;
        }

        _ = ApplySelectionAsync(value);
    }

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorText;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>实时（本月）数据。</summary>
    [ObservableProperty]
    private TrailblazeCalendarSummary? _currentSummary;

    public bool HasCurrent => CurrentSummary is not null;

    /// <summary>历史（选中月）数据。</summary>
    [ObservableProperty]
    private TrailblazeCalendarSummary? _selectSummary;

    public bool HasSelect => SelectSummary is not null;

    public ObservableCollection<IncomeSeriesItem> CurrentSeries { get; } = [];

    public ObservableCollection<IncomeSeriesItem> SelectSeries { get; } = [];

    public ObservableCollection<TrailblazeDayItem> DayList { get; } = [];

    /// <summary>「获取明细」的结果（选中月两类明细合并后按时间倒序）。</summary>
    public ObservableCollection<TrailblazeDetailItem> Details { get; } = [];

    [ObservableProperty]
    private bool _isDetailsOpen;

    public bool HasDetails => Details.Count > 0;

    // ── 实时数据卡文案 ────────────────────────────────────────────────
    public string CurrentDayHcoin => (CurrentSummary?.DayData?.CurrentHcoin ?? 0).ToString(CultureInfo.InvariantCulture);

    public string CurrentDayRailsPass => (CurrentSummary?.DayData?.CurrentRailsPass ?? 0).ToString(CultureInfo.InvariantCulture);

    public string CurrentMonthHcoin => (CurrentSummary?.MonthData?.CurrentHcoin ?? 0).ToString(CultureInfo.InvariantCulture);

    public string CurrentMonthRailsPass => (CurrentSummary?.MonthData?.CurrentRailsPass ?? 0).ToString(CultureInfo.InvariantCulture);

    // ── 历史数据卡文案 ────────────────────────────────────────────────
    public string SelectMonthText => SelectSummary is { } summary ? FormatMonth(summary.Month) : "";

    public string SelectMonthHcoin => (SelectSummary?.MonthData?.CurrentHcoin ?? 0).ToString(CultureInfo.InvariantCulture);

    public string SelectMonthRailsPass => (SelectSummary?.MonthData?.CurrentRailsPass ?? 0).ToString(CultureInfo.InvariantCulture);


    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_initialized)
        {
            await InitializeAsync();
        }
    }


    [RelayCommand]
    private async Task GetDetailsAsync(string? month)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            return;
        }

        try
        {
            IsDetailsOpen = true;
            Details.Clear();
            var jade = await _client.GetTrailblazeCalendarDetailAsync(_role, month, 1).ConfigureAwait(true);
            var pass = await _client.GetTrailblazeCalendarDetailAsync(_role, month, 2).ConfigureAwait(true);

            foreach (var item in (jade?.List ?? []).Concat(pass?.List ?? []).OrderByDescending(x => x.Time))
            {
                Details.Add(new TrailblazeDetailItem(item));
            }

            OnPropertyChanged(nameof(HasDetails));
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["TrailblazeCalendar.LoadFailed"], ex.Message);
        }
    }


    private async Task InitializeAsync()
    {
        _initialized = true;

        if (ToolboxDemo.Enabled)
        {
            ApplyDemo();
            return;
        }

        IsBusy = true;
        ErrorText = null;
        try
        {
            var summary = await _client.GetTrailblazeCalendarSummaryAsync(_role).ConfigureAwait(true);
            CurrentSummary = summary;
            CacheSummary(summary);
            RebuildMonthList(summary.Month);

            ShowEmpty = MonthList.Count == 0;
            if (ShowEmpty)
            {
                return;
            }

            var target = MonthList.FirstOrDefault(x => x.Month == summary.Month) ?? MonthList[0];
            _applyingSelection = true;
            SelectedMonth = target;
            _applyingSelection = false;
            ApplySelection(target);
            await LoadDaysAsync(target.Month).ConfigureAwait(true);
        }
        catch (miHoYoApiException ex)
        {
            ErrorText = string.Format(Strings["TrailblazeCalendar.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorText = string.Format(Strings["TrailblazeCalendar.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["TrailblazeCalendar.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        finally
        {
            IsBusy = false;
        }
    }


    private void CacheSummary(TrailblazeCalendarSummary summary)
    {
        if (!string.IsNullOrWhiteSpace(summary.Month))
        {
            _summaries[summary.Month] = summary;
        }
    }


    private void RebuildMonthList(string? selectMonth)
    {
        MonthList.Clear();
        var months = new List<string>();
        if (CurrentSummary?.OptionalMonth is { Count: > 0 } optional)
        {
            months.AddRange(optional);
        }

        if (CurrentSummary?.Month is { Length: > 0 } current && !months.Contains(current))
        {
            months.Add(current);
        }

        foreach (var month in months.OrderByDescending(x => x, StringComparer.Ordinal))
        {
            var item = new TrailblazeMonthItem(month);
            if (_summaries.TryGetValue(month, out var summary))
            {
                item.Hcoin = summary.MonthData?.CurrentHcoin ?? 0;
            }

            MonthList.Add(item);
        }

        _ = selectMonth;
    }


    private async Task ApplySelectionAsync(TrailblazeMonthItem? item)
    {
        if (item is null)
        {
            return;
        }

        ApplySelection(item);
        await LoadDaysAsync(item.Month).ConfigureAwait(true);
    }


    private void ApplySelection(TrailblazeMonthItem? item)
    {
        if (item is null)
        {
            SelectSummary = null;
            SelectSeries.Clear();
            return;
        }

        if (!_summaries.TryGetValue(item.Month, out var summary))
        {
            _ = FetchMonthSummaryAsync(item.Month);
            SelectSummary = null;
            SelectSeries.Clear();
            return;
        }

        SelectSummary = summary;
        FillSeries(SelectSeries, summary);
        if (CurrentSummary is { } current)
        {
            FillSeries(CurrentSeries, current);
        }

        NotifySelectTexts();
    }


    private async Task FetchMonthSummaryAsync(string month)
    {
        try
        {
            var summary = await _client.GetTrailblazeCalendarSummaryAsync(_role, month).ConfigureAwait(true);
            CacheSummary(summary);
            if (SelectedMonth?.Month == month)
            {
                ApplySelection(SelectedMonth);
            }

            var item = MonthList.FirstOrDefault(x => x.Month == month);
            if (item is not null && summary.MonthData is { } data)
            {
                item.Hcoin = data.CurrentHcoin;
                OnPropertyChanged(nameof(MonthList));
            }
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["TrailblazeCalendar.LoadFailed"], ex.Message);
        }
    }


    private void FillSeries(ObservableCollection<IncomeSeriesItem> target, TrailblazeCalendarSummary summary)
    {
        target.Clear();
        if (summary.MonthData?.GroupBy is not { Count: > 0 } groups)
        {
            return;
        }

        foreach (var group in groups)
        {
            target.Add(new IncomeSeriesItem(group.ActionName, group.Percent, ActionColors.GetValueOrDefault(group.Action, Colors.Gray)));
        }
    }


    /// <summary>
    /// Starward <c>RefreshDailyDataPlot</c>：按天累加两类明细的 <c>Number</c>，
    /// 各自除以当月最大值（全 0 按 0 显示），得到两条进度条的值。
    /// </summary>
    private async Task LoadDaysAsync(string? month)
    {
        DayList.Clear();
        if (string.IsNullOrWhiteSpace(month) || month.Length < 6)
        {
            return;
        }

        _detailCts?.Cancel();
        _detailCts = new CancellationTokenSource();
        var token = _detailCts.Token;

        try
        {
            var jade = await _client.GetTrailblazeCalendarDetailAsync(_role, month, 1, 100, token).ConfigureAwait(true);
            var pass = await _client.GetTrailblazeCalendarDetailAsync(_role, month, 2, 100, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            var year = int.Parse(month[..4], CultureInfo.InvariantCulture);
            var mon = int.Parse(month[4..6], CultureInfo.InvariantCulture);
            var days = DateTime.DaysInMonth(year, mon);
            var jadeStats = new int[days];
            foreach (var item in jade?.List ?? [])
            {
                if (item.Time.Month == mon && item.Time.Day <= days)
                {
                    jadeStats[item.Time.Day - 1] += item.Number;
                }
            }

            var passStats = new int[days];
            foreach (var item in pass?.List ?? [])
            {
                if (item.Time.Month == mon && item.Time.Day <= days)
                {
                    passStats[item.Time.Day - 1] += item.Number;
                }
            }

            var maxJade = (double)Math.Max(jadeStats.DefaultIfEmpty(0).Max(), 1);
            var maxPass = (double)Math.Max(passStats.DefaultIfEmpty(0).Max(), 1);
            for (var i = 0; i < days; i++)
            {
                DayList.Add(new TrailblazeDayItem
                {
                    Day = $"{month[4..]}-{i + 1:D2}",
                    Jade = jadeStats[i],
                    RailsPass = passStats[i],
                    JadeProgress = jadeStats[i] / maxJade,
                    RailsPassProgress = passStats[i] / maxPass,
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["TrailblazeCalendar.LoadFailed"], ex.Message);
        }
    }


    private void NotifySelectTexts()
    {
        OnPropertyChanged(nameof(HasCurrent));
        OnPropertyChanged(nameof(HasSelect));
        OnPropertyChanged(nameof(CurrentDayHcoin));
        OnPropertyChanged(nameof(CurrentDayRailsPass));
        OnPropertyChanged(nameof(CurrentMonthHcoin));
        OnPropertyChanged(nameof(CurrentMonthRailsPass));
        OnPropertyChanged(nameof(SelectMonthText));
        OnPropertyChanged(nameof(SelectMonthHcoin));
        OnPropertyChanged(nameof(SelectMonthRailsPass));
    }


    private static string FormatMonth(string? month)
        => month is { Length: 6 } && DateTime.TryParseExact(month, "yyyyMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : month ?? "";


    /// <summary>
    /// 截图夹具（<c>LUNARWARD_TOOLBOX_DEMO=1</c>）：两月真实结构演示数据，
    /// 收入组成四条、每日数据按正弦式波动铺满当月。
    /// </summary>
    private void ApplyDemo()
    {
        var now = DateTimeOffset.Now;
        var month = now.ToString("yyyyMM", CultureInfo.InvariantCulture);

        CurrentSummary = new TrailblazeCalendarSummary
        {
            Uid = _role.Uid,
            Month = month,
            DataMonth = month,
            OptionalMonth = [month, now.AddMonths(-1).ToString("yyyyMM", CultureInfo.InvariantCulture)],
            DayData = new TrailblazeCalendarDayData { CurrentHcoin = 120, CurrentRailsPass = 1, LastHcoin = 480, LastRailsPass = 0 },
            MonthData = new TrailblazeCalendarMonthData
            {
                Month = month,
                CurrentHcoin = 3420,
                CurrentRailsPass = 5,
                HcoinRate = 61,
                RailsRate = 55,
                GroupBy = BuildDemoGroups(),
            },
        };
        CacheSummary(CurrentSummary);

        RebuildMonthList(month);
        ShowEmpty = false;
        _applyingSelection = true;
        SelectedMonth = MonthList[0];
        _applyingSelection = false;
        ApplySelection(SelectedMonth);

        var year = now.Year;
        var mon = now.Month;
        var days = DateTime.DaysInMonth(year, mon);
        for (var i = 0; i < days; i++)
        {
            var jade = 60 + (int)(Math.Sin(i * 0.7) * 40) + (i % 5 == 0 ? 120 : 0);
            var pass = (i * 7) % 3 == 0 ? 150 : 0;
            DayList.Add(new TrailblazeDayItem
            {
                Day = $"{mon:D2}-{i + 1:D2}",
                Jade = jade,
                RailsPass = pass,
                JadeProgress = jade / 220.0,
                RailsPassProgress = pass / 150.0,
            });
        }
    }


    private static List<TrailblazeCalendarMonthDataGroupBy> BuildDemoGroups() =>
    [
        new() { Action = "daily_reward", ActionName = "每日实训", Num = 1680, Percent = 49 },
        new() { Action = "space_reward", ActionName = "模拟宇宙", Num = 900, Percent = 26 },
        new() { Action = "event_reward", ActionName = "活动奖励", Num = 500, Percent = 15 },
        new() { Action = "other", ActionName = "其他", Num = 340, Percent = 10 },
    ];
}


/// <summary>左栏的一期月份（Starward <c>ListView_MonthDataList</c> 行：yyyy-MM + 星琼数）。</summary>
public sealed partial class TrailblazeMonthItem : ObservableObject
{
    public TrailblazeMonthItem(string month)
    {
        Month = month;
        MonthText = month is { Length: 6 } ? $"{month[..4]}-{month[4..]}" : month;
    }

    public string Month { get; }

    public string MonthText { get; }

    [ObservableProperty]
    private int _hcoin;

    public string HcoinText => Hcoin.ToString(CultureInfo.InvariantCulture);
}


/// <summary>每日数据的一行（源码 <c>CalendarDayData</c>）。</summary>
public sealed partial class TrailblazeDayItem : ObservableObject
{
    public string Day { get; init; } = "";

    public int Jade { get; init; }

    public int RailsPass { get; init; }

    public double JadeProgress { get; init; }

    public double RailsPassProgress { get; init; }

    public string JadeText => Jade.ToString(CultureInfo.InvariantCulture);

    public string RailsPassText => RailsPass.ToString(CultureInfo.InvariantCulture);
}


/// <summary>明细列表的一行（源码 <c>Get Details</c> 打开的面板）。</summary>
public sealed class TrailblazeDetailItem
{
    public TrailblazeDetailItem(TrailblazeCalendarDetailItem item)
    {
        TimeText = item.Time.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        ActionName = item.ActionName;
        NumberText = item.Type == 1 ? $"+ {item.Number}" : $"+ {item.Number}";
        TypeText = item.Type == 1 ? "星琼" : "古老梦华";
    }

    public string TimeText { get; }

    public string ActionName { get; }

    public string NumberText { get; }

    public string TypeText { get; }
}
