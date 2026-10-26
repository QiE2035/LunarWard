using LunarWard.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.ZZZ.InterKnotReport;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 绳网月报页 VM（Starward <c>Features/GameRecord/ZZZ/InterKnotMonthlyReportPage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 左侧是可选月份（Starward 从 SQLite 缓存读；LunarWard 用「实时 summary + 明细」两个 API，
/// 结果缓存到本页字典里，刷新/取明细后重建列表），右侧是实时数据、选中月份的历史数据与每日数据。
/// </para>
/// <para>
/// 菲林/母带/邦布券的图标在 Starward 里是 PNG 资源（IconCurrency/GachaTicket2Big/GachaTicket3Big），
/// LunarWard 未打包这些素材，用同色字形占位并在 XAML 注释；百分比条是自绘控件
/// <see cref="Views.Starward.Controls.ColorRectChart"/>（Starward <c>sc:ColorRectChart</c> 的等价物）。
/// </para>
/// </remarks>
public sealed partial class InterKnotMonthlyReportPageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>Starward <c>actionColorMap</c>：菲林收入来源的固定配色。</summary>
    private static readonly Dictionary<string, Color> ActionColors = new(StringComparer.Ordinal)
    {
        ["daily_activity_rewards"] = Color.Parse("#5CC83D"),
        ["growth_rewards"] = Color.Parse("#A2D104"),
        ["event_rewards"] = Color.Parse("#FFDE00"),
        ["hollow_rewards"] = Color.Parse("#FF4483"),
        ["shiyu_rewards"] = Color.Parse("#57BFF7"),
        ["mail_rewards"] = Color.Parse("#C92ADE"),
        ["other_rewards"] = Color.Parse("#F1AD3D"),
    };

    /// <summary>收入来源的本地化键（Starward <c>InterKnotMonthlyReportPage_*</c>）。</summary>
    private static readonly Dictionary<string, string> ActionNames = new(StringComparer.Ordinal)
    {
        ["daily_activity_rewards"] = "InterKnot.DailyActivityRewards",
        ["growth_rewards"] = "InterKnot.DevelopmentRewards",
        ["event_rewards"] = "InterKnot.EventRewards",
        ["hollow_rewards"] = "InterKnot.HollowZeroRewards",
        ["shiyu_rewards"] = "InterKnot.ShiyuDefenseRewards",
        ["mail_rewards"] = "InterKnot.MailRewards",
        ["other_rewards"] = "InterKnot.OtherRewards",
    };

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    private bool _initialized;

    private readonly Dictionary<string, InterKnotReportSummary> _summaries = new(StringComparer.Ordinal);

    private readonly Dictionary<(string Month, string Type), List<InterKnotReportDetailItem>> _details = new();


    public InterKnotMonthlyReportPageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _ = InitializeAsync();
    }


    /// <summary>左侧月份列表（Starward <c>ListView_MonthDataList</c>）。</summary>
    public ObservableCollection<InterKnotMonthItem> MonthList { get; } = [];

    [ObservableProperty]
    private InterKnotMonthItem? _selectedMonth;

    partial void OnSelectedMonthChanged(InterKnotMonthItem? value) => ApplySelection();


    /// <summary>实时数据（Starward <c>CurrentSummary</c>，当前月）。</summary>
    [ObservableProperty]
    private InterKnotReportSummary? _currentSummary;

    partial void OnCurrentSummaryChanged(InterKnotReportSummary? value)
    {
        OnPropertyChanged(nameof(HasCurrent));
        CurrentAwards.Clear();
        foreach (var award in BuildAwards(value))
        {
            CurrentAwards.Add(award);
        }

        CurrentSeries.Clear();
        foreach (var series in BuildSeries(value))
        {
            CurrentSeries.Add(series);
        }
    }

    public bool HasCurrent => CurrentSummary is not null;


    /// <summary>选中月份的历史数据（Starward <c>SelectMonthData</c>）。</summary>
    [ObservableProperty]
    private InterKnotReportSummary? _selectMonthData;

    partial void OnSelectMonthDataChanged(InterKnotReportSummary? value) => OnPropertyChanged(nameof(HasSelect));

    public bool HasSelect => SelectMonthData is not null;


    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorText;

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public bool HasError => ErrorText is not null;


    public ObservableCollection<IncomeSeriesItem> CurrentSeries { get; } = [];

    public ObservableCollection<IncomeSeriesItem> SelectSeries { get; } = [];

    public ObservableCollection<InterKnotAwardItem> CurrentAwards { get; } = [];

    public ObservableCollection<InterKnotAwardItem> SelectAwards { get; } = [];

    public ObservableCollection<InterKnotDayItem> DayList { get; } = [];

    /// <summary>「获取明细」Flyout 的月份项（Starward 的 <c>OptionalMonth</c>）。</summary>
    public List<string> OptionalMonths { get; } = [];


    // ── 命令 ─────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_initialized)
        {
            await InitializeAsync();
        }
    }


    /// <summary>
    /// 「获取明细」：Starward 先取该月 summary，再逐个 award 的 data_type 拉明细，
    /// 然后重建月份列表（此时列表项的菲林数才是完整月数据）。
    /// </summary>
    public async Task LoadMonthDetailsAsync(string month)
    {
        if (string.IsNullOrEmpty(month))
        {
            return;
        }

        IsBusy = true;
        ErrorText = null;
        try
        {
            var summary = await _client.GetInterKnotReportSummaryAsync(_role, month);
            CacheSummary(summary);
            foreach (var award in summary.MonthData.List)
            {
                try
                {
                    var detail = await _client.GetInterKnotReportDetailAsync(_role, month, award.DataType);
                    _details[(month, award.DataType)] = detail.List ?? [];
                }
                catch (miHoYoApiException)
                {
                    // 某个类别没数据是正常情况（例如当月没有邦布券）。
                }
            }

            RebuildMonthList(month);
            ShowEmpty = MonthList.Count == 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorText = string.Format(Strings["InterKnot.NetworkError"], ex.Message);
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["InterKnot.LoadFailed"], ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }


    private async Task InitializeAsync()
    {
        _initialized = true;

        if (ToolboxDemo.Enabled)
        {
            ApplyDemoReport();
            return;
        }

        IsBusy = true;
        ErrorText = null;
        try
        {
            var summary = await _client.GetInterKnotReportSummaryAsync(_role);
            CacheSummary(summary);
            CurrentSummary = summary;
            FillOptionalMonths(summary);
            RebuildMonthList(summary.DataMonth);
            ShowEmpty = MonthList.Count == 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorText = string.Format(Strings["InterKnot.NetworkError"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["InterKnot.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        finally
        {
            IsBusy = false;
        }
    }


    private void CacheSummary(InterKnotReportSummary summary)
    {
        if (summary is null || string.IsNullOrEmpty(summary.DataMonth))
        {
            return;
        }

        _summaries[summary.DataMonth] = summary;
    }


    private void FillOptionalMonths(InterKnotReportSummary summary)
    {
        OptionalMonths.Clear();
        if (summary.OptionalMonth is { Count: > 0 })
        {
            OptionalMonths.AddRange(summary.OptionalMonth);
        }
        else if (!string.IsNullOrEmpty(summary.DataMonth))
        {
            OptionalMonths.Add(summary.DataMonth);
        }
    }


    private void RebuildMonthList(string? selectMonth)
    {
        var targetMonth = selectMonth ?? SelectedMonth?.Summary.DataMonth;
        MonthList.Clear();
        foreach (var summary in _summaries.Values.OrderByDescending(x => x.DataMonth, StringComparer.Ordinal))
        {
            MonthList.Add(new InterKnotMonthItem(summary));
        }

        SelectedMonth = MonthList.FirstOrDefault(x => x.Summary.DataMonth == targetMonth)
                        ?? MonthList.FirstOrDefault();
    }


    private void ApplySelection()
    {
        var summary = SelectedMonth?.Summary;
        SelectMonthData = summary;

        SelectAwards.Clear();
        foreach (var award in BuildAwards(summary))
        {
            SelectAwards.Add(award);
        }

        SelectSeries.Clear();
        foreach (var series in BuildSeries(summary))
        {
            SelectSeries.Add(series);
        }

        DayList.Clear();
        if (summary is not null)
        {
            foreach (var day in BuildDays(summary))
            {
                DayList.Add(day);
            }
        }
    }


    // ── 数据整形 ─────────────────────────────────────────────────────

    /// <summary>Starward <c>GetPolychromeCount</c>：月份列表第二行显示的菲林数。</summary>
    public static int GetPolychromeCount(InterKnotReportSummary? summary) =>
        summary?.MonthData?.List?.FirstOrDefault(a => a.DataType == InterKnotReportDataType.PolychromesData)?.Count ?? 0;


    private List<InterKnotAwardItem> BuildAwards(InterKnotReportSummary? summary)
    {
        var list = new List<InterKnotAwardItem>();
        if (summary?.MonthData?.List is not { } awards)
        {
            return list;
        }

        foreach (var award in awards)
        {
            var (glyph, color) = GlyphFor(award.DataType);
            // Starward 直接绑定接口返回的 DataName（不随 UI 语言翻译）。
            list.Add(new InterKnotAwardItem(award.DataName, award.Count.ToString(CultureInfo.InvariantCulture), glyph, color));
        }

        return list;
    }


    private List<IncomeSeriesItem> BuildSeries(InterKnotReportSummary? summary)
    {
        var list = new List<IncomeSeriesItem>();
        if (summary?.MonthData?.IncomeComponents is not { } components)
        {
            return list;
        }

        foreach (var component in components)
        {
            var name = ActionNames.TryGetValue(component.Action, out var key) ? Strings[key] : component.Action;
            var color = ActionColors.GetValueOrDefault(component.Action, Colors.Gray);
            list.Add(new IncomeSeriesItem(name, component.Percent, color));
        }

        return list;
    }


    /// <summary>
    /// Starward <c>RefreshDailyDataPlot</c>：按天累加三类明细的 <c>num</c>，
    /// 每类都除以当月最大值（全 0 时按 0 显示），得到三条进度条的值。
    /// </summary>
    private List<InterKnotDayItem> BuildDays(InterKnotReportSummary summary)
    {
        var month = summary.DataMonth ?? "";
        if (month.Length < 6
            || !int.TryParse(month[..4], CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(month[4..], CultureInfo.InvariantCulture, out var monthNumber))
        {
            return [];
        }

        var days = DateTime.DaysInMonth(year, monthNumber);
        var poly = new int[days];
        var tape = new int[days];
        var boopon = new int[days];

        Accumulate(month, InterKnotReportDataType.PolychromesData, poly);
        Accumulate(month, InterKnotReportDataType.MatserTapeData, tape);
        Accumulate(month, InterKnotReportDataType.BooponsData, boopon);

        var maxPoly = poly.Max();
        var maxTape = tape.Max();
        var maxBoopon = boopon.Max();

        var list = new List<InterKnotDayItem>(days);
        for (var i = 0; i < days; i++)
        {
            list.Add(new InterKnotDayItem(
                $"{month[4..]}-{i + 1:D2}",
                poly[i],
                maxPoly == 0 ? 0 : (double)poly[i] / maxPoly,
                tape[i],
                maxTape == 0 ? 0 : (double)tape[i] / maxTape,
                boopon[i],
                maxBoopon == 0 ? 0 : (double)boopon[i] / maxBoopon));
        }

        return list;
    }


    private void Accumulate(string month, string type, int[] stats)
    {
        if (!_details.TryGetValue((month, type), out var items))
        {
            return;
        }

        foreach (var item in items)
        {
            var day = item.Time.LocalDateTime.Day;
            if (day >= 1 && day <= stats.Length)
            {
                stats[day - 1] += item.Number;
            }
        }
    }


    private static (string Glyph, Color Color) GlyphFor(string type) => type switch
    {
        InterKnotReportDataType.PolychromesData => ("\u2726", Color.Parse("#E8C47B")),
        InterKnotReportDataType.MatserTapeData => ("\u25A4", Color.Parse("#8FB8E8")),
        InterKnotReportDataType.BooponsData => ("\u25C9", Color.Parse("#F0D060")),
        _ => ("\u25A0", Color.Parse("#B0B0B0")),
    };


    // ── 自检夹具 ─────────────────────────────────────────────────────

    /// <summary>
    /// 自检/截图夹具（<c>LUNARWARD_TOOLBOX_DEMO=1</c>）：灌三个月演示数据，不联网。
    /// </summary>
    private void ApplyDemoReport()
    {
        var months = new[] { "202610", "202609", "202608" };
        foreach (var month in months)
        {
            var summary = BuildDemoSummary(month);
            _summaries[month] = summary;
            if (month == "202610")
            {
                _details[(month, InterKnotReportDataType.PolychromesData)] = DemoDetail(month, 1, 10, 600);
                _details[(month, InterKnotReportDataType.MatserTapeData)] = DemoDetail(month, 3, 0, 2);
                _details[(month, InterKnotReportDataType.BooponsData)] = DemoDetail(month, 5, 0, 1);
            }
        }

        CurrentSummary = _summaries["202610"];
        FillOptionalMonths(CurrentSummary);
        RebuildMonthList("202610");
        ShowEmpty = false;
        ErrorText = null;
    }


    private static InterKnotReportSummary BuildDemoSummary(string month)
    {
        return new InterKnotReportSummary
        {
            Uid = 100000001,
            Region = "prod_gf_cn",
            CurrentMonth = month,
            DataMonth = month,
            OptionalMonth = ["202610", "202609", "202608"],
            RoleInfo = new InterKnotReportRoleInfo(),
            MonthData = new InterKnotReportMonthData
            {
                List =
                [
                    new InterKnotReportSummaryAward { DataType = InterKnotReportDataType.PolychromesData, DataName = "菲林", Count = 6000 },
                    new InterKnotReportSummaryAward { DataType = InterKnotReportDataType.MatserTapeData, DataName = "母带", Count = 12 },
                    new InterKnotReportSummaryAward { DataType = InterKnotReportDataType.BooponsData, DataName = "邦布券", Count = 5 },
                ],
                IncomeComponents =
                [
                    new InterKnotReportIncomeComponent { Action = "daily_activity_rewards", Percent = 35 },
                    new InterKnotReportIncomeComponent { Action = "growth_rewards", Percent = 20 },
                    new InterKnotReportIncomeComponent { Action = "event_rewards", Percent = 15 },
                    new InterKnotReportIncomeComponent { Action = "hollow_rewards", Percent = 10 },
                    new InterKnotReportIncomeComponent { Action = "shiyu_rewards", Percent = 8 },
                    new InterKnotReportIncomeComponent { Action = "mail_rewards", Percent = 7 },
                    new InterKnotReportIncomeComponent { Action = "other_rewards", Percent = 5 },
                ],
            },
        };
    }


    private static List<InterKnotReportDetailItem> DemoDetail(string month, int day, int count, int number)
    {
        var list = new List<InterKnotReportDetailItem>(Math.Max(count, 1));
        for (var i = 0; i < Math.Max(count, 1); i++)
        {
            list.Add(new InterKnotReportDetailItem
            {
                Time = new DateTimeOffset(int.Parse(month[..4], CultureInfo.InvariantCulture), int.Parse(month[4..], CultureInfo.InvariantCulture), Math.Min(day + i, 28), 12, 0, 0, TimeSpan.FromHours(8)),
                Number = number,
            });
        }

        return list;
    }
}


/// <summary>月份列表项（Starward <c>InterKnotReportSummary</c> 的展示层）。</summary>
public sealed class InterKnotMonthItem(InterKnotReportSummary summary)
{
    public InterKnotReportSummary Summary { get; } = summary;

    public string DataMonth => Summary.DataMonth;

    public string PolychromeText =>
        LunarWard.ViewModels.Starward.InterKnotMonthlyReportPageViewModel.GetPolychromeCount(Summary)
            .ToString(CultureInfo.InvariantCulture);
}


/// <summary>菲林收入来源的一段（Starward <c>ColorRectChart.ChartLegend</c>）。</summary>
public sealed record IncomeSeriesItem(string Name, int Percent, Color Color)
{
    public string PercentText => $"{Percent}%";

    /// <summary>XAML 图例色块用（Starward 用 ColorToSolidColorBrushConverter）。</summary>
    public IBrush Brush => new SolidColorBrush(Color);
}


/// <summary>月度数据里的一项奖励（Starward <c>InterKnotReportSummaryAward</c> 的展示层）。</summary>
public sealed record InterKnotAwardItem(string Name, string CountText, string Glyph, Color GlyphColor)
{
    public IBrush GlyphBrush => new SolidColorBrush(GlyphColor);
}


/// <summary>每日数据行（Starward <c>CalendarDayData</c>）。</summary>
public sealed record InterKnotDayItem(
    string Day,
    int Poly,
    double PolyProgress,
    int Tape,
    double TapeProgress,
    int Boopon,
    double BooponProgress);
