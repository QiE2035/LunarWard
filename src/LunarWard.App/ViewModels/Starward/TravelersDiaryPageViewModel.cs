using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Controls;
using LunarWard.App.Views.Starward;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.Genshin.TravelersDiary;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 旅行者札记页 VM（Starward <c>Features/GameRecord/Genshin/TravelersDiaryPage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// Starward 的月份列表和每日明细来自 SQLite 缓存（<c>GameRecordService</c>）；LunarWard 没有全量
/// 缓存库，这里用当前总览的 <c>optional_month</c> 直接拼月份列表，选中某个月时按需拉该月总览和
/// 原石/摩拉明细（<c>GetTravelsDiaryDetailAsync(type=1/2)</c>），再按天聚合 —— 与源码的
/// <c>RefreshDailyDataPlot</c> 算法一致（每日进度 = 当天值 / 当月最大值）。
/// </para>
/// <para>
/// 图例颜色和 <c>actionColorMap</c> 与 Starward 相同（按 <c>action_id</c> 取 8 色之一）。
/// 原石 / 摩拉的小图标不在 LunarWard 打包资源里，用同色字形（◆ / ●）占位。
/// </para>
/// </remarks>
public sealed partial class TravelersDiaryPageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Starward <c>actionColorMap</c> 的 8 个图例颜色。</summary>
    private static readonly IBrush[] ActionColors =
    [
        new SolidColorBrush(Color.Parse("#FF72A7C6")),
        new SolidColorBrush(Color.Parse("#FFD46463")),
        new SolidColorBrush(Color.Parse("#FF6FB0B2")),
        new SolidColorBrush(Color.Parse("#FFBC9959")),
        new SolidColorBrush(Color.Parse("#FF72986F")),
        new SolidColorBrush(Color.Parse("#FF796BA6")),
        new SolidColorBrush(Color.Parse("#FF597D9F")),
        new SolidColorBrush(Color.Parse("#FF7A7CB2")),
    ];

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    /// <summary>自检/截图时的内存样例数据（<c>LUNARWARD_TOOLBOX_DEMO=1</c>）。</summary>
    private readonly bool _demo;

    private bool _suppressSelectionLoad;


    public TravelersDiaryPageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _demo = string.Equals(Environment.GetEnvironmentVariable("LUNARWARD_TOOLBOX_DEMO"), "1", StringComparison.Ordinal);
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _ = RefreshAsync();
    }


    // ── 左列：月份列表 ─────────────────────────────────────────────

    /// <summary>左侧月份卡列表（Starward <c>ListView_MonthDataList</c>）。</summary>
    public ObservableCollection<TravelersDiaryMonthItem> MonthDataList { get; } = [];

    [ObservableProperty]
    private TravelersDiaryMonthItem? _selectedMonth;

    partial void OnSelectedMonthChanged(TravelersDiaryMonthItem? value)
    {
        if (value is null)
        {
            HasSelectedMonth = false;
            return;
        }

        HasSelectedMonth = true;
        if (!_suppressSelectionLoad)
        {
            _ = LoadMonthAsync(value);
        }
    }

    /// <summary>「获取明细」Flyout 里的月份（Starward <c>MenuFlyout_GetDetails.Items</c>）。</summary>
    public ObservableCollection<TravelersDiaryMonthOption> OptionalMonths { get; } = [];


    // ── 状态 ───────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDetailsBusy;

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private bool _hasSelectedMonth;

    [ObservableProperty]
    private string? _errorText;

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public bool HasError => ErrorText is not null;


    // ── 实时数据卡 ─────────────────────────────────────────────────

    [ObservableProperty]
    private bool _hasCurrentSummary;

    [ObservableProperty]
    private int _dayPrimogems;

    [ObservableProperty]
    private int _dayMora;

    [ObservableProperty]
    private int _monthPrimogems;

    [ObservableProperty]
    private int _monthMora;

    [ObservableProperty]
    private IReadOnlyList<ChartLegend>? _currentSeries;


    // ── 历史数据卡（选中的月份） ────────────────────────────────────

    [ObservableProperty]
    private int _selectPrimogems;

    [ObservableProperty]
    private int _selectMora;

    [ObservableProperty]
    private IReadOnlyList<ChartLegend>? _selectSeries;

    /// <summary>「历史数据 2026-10」的标题（Starward 用多个 Run 拼，这里拼成一个字符串）。</summary>
    public string HistoryTitle => HasSelectedMonth && SelectedMonth is { } m
        ? $"{Strings["TravelersDiary.HistoricalData"]} {m.Year}-{m.Month:D2}"
        : Strings["TravelersDiary.HistoricalData"];

    /// <summary>「每日数据 2026-10」的标题。</summary>
    public string DailyDataTitle => HasSelectedMonth && SelectedMonth is { } m
        ? $"{Strings["TravelersDiary.DailyData"]} {m.Year}-{m.Month:D2}"
        : Strings["TravelersDiary.DailyData"];

    /// <summary>每日数据表（Starward <c>DayDataList</c>）。</summary>
    public ObservableCollection<TravelersDiaryDayItem> DayDataList { get; } = [];

    [ObservableProperty]
    private bool _hasDailyData;


    // ── 加载 ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_demo)
        {
            LoadDemo();
            return;
        }

        IsBusy = true;
        ErrorText = null;
        try
        {
            var summary = await _client.GetTravelsDiarySummaryAsync(_role).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() => ApplySummary(summary));
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ErrorText = string.Format(CultureInfo.CurrentCulture, Strings["TravelersDiary.LoadFailed"], ex.Message);
                ShowEmpty = MonthDataList.Count == 0;
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsBusy = false);
        }
    }


    /// <summary>把总览填进实时卡 + 月份列表（Starward <c>GetCurrentSummaryAsync</c> + <c>GetMonthDataList</c>）。</summary>
    private void ApplySummary(TravelersDiarySummary summary)
    {
        HasCurrentSummary = true;
        DayPrimogems = summary.DayData.CurrentPrimogems;
        DayMora = summary.DayData.CurrentMora;
        MonthPrimogems = summary.MonthData.CurrentPrimogems;
        MonthMora = summary.MonthData.CurrentMora;
        CurrentSeries = BuildSeries(summary.MonthData.PrimogemsGroupBy);

        OptionalMonths.Clear();
        foreach (var month in summary.OptionalMonth.OrderByDescending(x => x))
        {
            OptionalMonths.Add(new TravelersDiaryMonthOption(
                TravelersDiaryMonthItem.ResolveYear(month, summary.Date.Year, summary.CurrentMonth),
                month,
                new DateTime(2023, month, 1).ToString("MMM", CultureInfo.CurrentCulture)));
        }

        // Starward 从 SQLite 读所有已抓过的月份；这里只有当前月有完整数据，其余等选中时再拉。
        var current = MonthDataList.FirstOrDefault(x => x.Year == summary.MonthData.Year && x.Month == summary.MonthData.Month);
        if (current is null)
        {
            current = new TravelersDiaryMonthItem(summary.MonthData.Year, summary.MonthData.Month);
            var index = 0;
            while (index < MonthDataList.Count && MonthDataList[index].SortKey > current.SortKey)
            {
                index++;
            }
            MonthDataList.Insert(index, current);
        }
        current.Apply(summary.MonthData);
        if (SelectedMonth is null)
        {
            SelectedMonth = current;
        }
        ShowEmpty = MonthDataList.Count == 0;
    }


    /// <summary>选中某个月 → 拉该月总览 + 原石/摩拉明细，再重建每日表。</summary>
    private async Task LoadMonthAsync(TravelersDiaryMonthItem item)
    {
        if (item.IsLoaded && item.HasDetails)
        {
            ApplyMonth(item);
            return;
        }

        if (_demo)
        {
            ApplyMonth(item);
            return;
        }

        IsDetailsBusy = true;
        ErrorText = null;
        try
        {
            var summary = await _client.GetTravelsDiarySummaryAsync(_role, item.Month).ConfigureAwait(false);
            var primogems = await _client.GetTravelsDiaryDetailAsync(_role, item.Month, 1).ConfigureAwait(false);
            var mora = await _client.GetTravelsDiaryDetailAsync(_role, item.Month, 2).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                item.Apply(summary.MonthData);
                item.DetailItems.Clear();
                item.DetailItems.AddRange(primogems.List ?? []);
                item.DetailItems.AddRange(mora.List ?? []);
                item.HasDetails = true;
                if (ReferenceEquals(SelectedMonth, item))
                {
                    ApplyMonth(item);
                }
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                ErrorText = string.Format(CultureInfo.CurrentCulture, Strings["TravelersDiary.LoadFailed"], ex.Message));
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsDetailsBusy = false);
        }
    }


    /// <summary>把选中月份的数据铺到历史卡 + 每日数据表。</summary>
    private void ApplyMonth(TravelersDiaryMonthItem item)
    {
        SelectPrimogems = item.Primogems;
        SelectMora = item.Mora;
        SelectSeries = BuildSeries(item.MonthData?.PrimogemsGroupBy);
        OnPropertyChanged(nameof(HistoryTitle));
        OnPropertyChanged(nameof(DailyDataTitle));
        RefreshDailyDataPlot(item);
    }


    /// <summary>Starward <c>RefreshDailyDataPlot</c>：按天累加两种明细，进度 = 当天 / 当月最大。</summary>
    private void RefreshDailyDataPlot(TravelersDiaryMonthItem item)
    {
        DayDataList.Clear();
        if (item.Month <= 0 || item.Month > 12 || item.Year < 2000)
        {
            HasDailyData = false;
            return;
        }

        int days = DateTime.DaysInMonth(item.Year, item.Month);
        var primogems = new int[days];
        var mora = new int[days];
        foreach (var detail in item.DetailItems)
        {
            int day = detail.Time.Day;
            if (day < 1 || day > days)
            {
                continue;
            }
            if (detail.Type == 1)
            {
                primogems[day - 1] += detail.Number;
            }
            else if (detail.Type == 2)
            {
                mora[day - 1] += detail.Number;
            }
        }

        double maxPrimogems = primogems.Max();
        double maxMora = mora.Max();
        maxPrimogems = maxPrimogems == 0 ? double.MaxValue : maxPrimogems;
        maxMora = maxMora == 0 ? double.MaxValue : maxMora;

        for (int i = 0; i < days; i++)
        {
            DayDataList.Add(new TravelersDiaryDayItem
            {
                Day = $"{item.Month:D2}-{i + 1:D2}",
                Primogems = primogems[i],
                Mora = mora[i],
                PrimogemsProgress = primogems[i] / maxPrimogems,
                MoraProgress = mora[i] / maxMora,
            });
        }

        HasDailyData = DayDataList.Count > 0;
    }


    /// <summary>「获取明细」Flyout 的月份按钮（Starward 的 <c>MenuFlyoutItem.CommandParameter</c>）。</summary>
    [RelayCommand]
    private async Task FetchMonth(TravelersDiaryMonthOption? option)
    {
        if (option is null)
        {
            return;
        }

        var item = MonthDataList.FirstOrDefault(x => x.Year == option.Year && x.Month == option.Month);
        if (item is null)
        {
            item = new TravelersDiaryMonthItem(option.Year, option.Month);
            MonthDataList.Insert(0, item);
        }

        SelectedMonth = item;
        await LoadMonthAsync(item);
    }


    /// <summary>点月份卡（Starward 用 SelectionChanged；Avalonia 用 ListBox.SelectedItem 绑定）。</summary>
    [RelayCommand]
    private void SelectMonth(TravelersDiaryMonthItem? item) => SelectedMonth = item;


    private static IReadOnlyList<ChartLegend> BuildSeries(List<TravelersDiaryPrimogemsMonthGroupStats>? groupBy)
    {
        if (groupBy is null || groupBy.Count == 0)
        {
            return [];
        }

        return groupBy.Select(x => new ChartLegend(
            x.ActionName ?? "",
            x.Percent,
            ActionColors[Math.Clamp(x.ActionId, 0, ActionColors.Length - 1)])).ToList();
    }


    // ── 自检样例数据 ───────────────────────────────────────────────

    private void LoadDemo()
    {
        var summary = new TravelersDiarySummary
        {
            Date = new DateTime(2026, 10, 24, 9, 36, 31, DateTimeKind.Local),
            CurrentMonth = 10,
            DataMonth = 10,
            OptionalMonth = [8, 9, 10],
            DayData = new TravelersDiaryDayData { CurrentPrimogems = 160, CurrentMora = 48000 },
            MonthData = new TravelersDiaryMonthData
            {
                Year = 2026,
                Month = 10,
                CurrentPrimogems = 12800,
                CurrentMora = 1234567,
                PrimogemsGroupBy =
                [
                    new TravelersDiaryPrimogemsMonthGroupStats { ActionId = 0, ActionName = "每日委托", Number = 4200, Percent = 33 },
                    new TravelersDiaryPrimogemsMonthGroupStats { ActionId = 1, ActionName = "深境螺旋", Number = 1800, Percent = 14 },
                    new TravelersDiaryPrimogemsMonthGroupStats { ActionId = 2, ActionName = "活动任务", Number = 3600, Percent = 28 },
                    new TravelersDiaryPrimogemsMonthGroupStats { ActionId = 3, ActionName = "冒险任务", Number = 1200, Percent = 9 },
                    new TravelersDiaryPrimogemsMonthGroupStats { ActionId = 4, ActionName = "邮件", Number = 1500, Percent = 12 },
                    new TravelersDiaryPrimogemsMonthGroupStats { ActionId = 5, ActionName = "其他", Number = 500, Percent = 4 },
                ],
            },
        };

        ApplySummary(summary);
        MonthDataList.Clear();
        var october = new TravelersDiaryMonthItem(2026, 10);
        var september = new TravelersDiaryMonthItem(2026, 9);
        var august = new TravelersDiaryMonthItem(2026, 8);
        october.Apply(summary.MonthData);
        september.Apply(new TravelersDiaryMonthData { Year = 2026, Month = 9, CurrentPrimogems = 11540, CurrentMora = 1098765, PrimogemsGroupBy = summary.MonthData.PrimogemsGroupBy });
        august.Apply(new TravelersDiaryMonthData { Year = 2026, Month = 8, CurrentPrimogems = 13620, CurrentMora = 1564321, PrimogemsGroupBy = summary.MonthData.PrimogemsGroupBy });
        MonthDataList.Add(october);
        MonthDataList.Add(september);
        MonthDataList.Add(august);

        var random = new Random(20261024);
        foreach (var day in Enumerable.Range(1, 24))
        {
            october.DetailItems.Add(new TravelersDiaryAwardItem { Type = 1, Time = new DateTime(2026, 10, day), Number = random.Next(4, 8) * 60 });
            october.DetailItems.Add(new TravelersDiaryAwardItem { Type = 2, Time = new DateTime(2026, 10, day), Number = random.Next(0, 5) * 3600 });
        }
        october.HasDetails = true;
        _suppressSelectionLoad = true;
        SelectedMonth = october;
        _suppressSelectionLoad = false;
        ApplyMonth(october);
        foreach (var month in summary.OptionalMonth.OrderByDescending(x => x))
        {
            OptionalMonths.Add(new TravelersDiaryMonthOption(
                TravelersDiaryMonthItem.ResolveYear(month, summary.Date.Year, summary.CurrentMonth),
                month,
                new DateTime(2023, month, 1).ToString("MMM", CultureInfo.CurrentCulture)));
        }
        ShowEmpty = false;
    }
}


/// <summary>左侧的一个月份卡（Starward <c>ListView_MonthDataList</c> 的数据项）。</summary>
public sealed partial class TravelersDiaryMonthItem : ObservableObject
{
    public TravelersDiaryMonthItem(int year, int month)
    {
        Year = year;
        Month = month;
    }


    public int Year { get; }

    public int Month { get; }

    public int SortKey => Year * 100 + Month;

    public string MonthText => $"{Year}-{Month:D2}";

    [ObservableProperty]
    private int _primogems;

    [ObservableProperty]
    private bool _isLoaded;

    /// <summary>已拉过每日明细（Starward 里是 SQLite 有无明细表）。</summary>
    public bool HasDetails { get; set; }

    public TravelersDiaryMonthData? MonthData { get; private set; }

    public List<TravelersDiaryAwardItem> DetailItems { get; } = [];


    public void Apply(TravelersDiaryMonthData? data)
    {
        MonthData = data;
        Primogems = data?.CurrentPrimogems ?? 0;
        IsLoaded = data is not null;
        Mora = data?.CurrentMora ?? 0;
        OnPropertyChanged(nameof(PrimogemsText));
    }


    [ObservableProperty]
    private int _mora;

    public string PrimogemsText => IsLoaded ? Primogems.ToString(CultureInfo.CurrentCulture) : "—";


    /// <summary>可选月份所属年份（米游社只给月份：比当前月大则为上一年）。</summary>
    public static int ResolveYear(int month, int currentYear, int currentMonth)
        => month > currentMonth ? currentYear - 1 : currentYear;
}


/// <summary>「获取明细」Flyout 里的一项月份。</summary>
public sealed class TravelersDiaryMonthOption(int year, int month, string label)
{
    public int Year { get; } = year;

    public int Month { get; } = month;

    public string Label { get; } = label;

    public string MonthText => $"{Year}-{Month:D2}";
}


/// <summary>每日数据表的一行（Starward <c>DiaryDayData</c>）。</summary>
public sealed class TravelersDiaryDayItem
{
    public string Day { get; init; } = "";

    public int Primogems { get; init; }

    public int Mora { get; init; }

    public double PrimogemsProgress { get; init; }

    public double MoraProgress { get; init; }
}
