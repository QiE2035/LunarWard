using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LunarWard.Controls;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.StarRail.TrailblazeCalendar;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media;
using System.ComponentModel;


namespace LunarWard.Features.GameRecord.StarRail;

public sealed partial class TrailblazeCalendarPage : PageBase
{


    // Linux: 上游 x:Name 挂在 MenuFlyout 上由 XAML 编译器生成字段；
    // Avalonia 的 Flyout 不在页面命名域里，改为从所属按钮的 Flyout 属性取，语义相同。
    private MenuFlyout MenuFlyout_GetDetails => (MenuFlyout)Button_GetDetails.Flyout!;

    private readonly ILogger<TrailblazeCalendarPage> _logger = AppConfig.GetLogger<TrailblazeCalendarPage>();


    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    public TrailblazeCalendarPage()
    {
        this.InitializeComponent();
    }


    private GameRecordRole gameRole;


    public override void OnNavigatedTo(object? e)
    {
        if (e is GameRecordRole role)
        {
            gameRole = role;
        }
    }


    protected override async void OnLoaded()
    {
        await Task.Delay(16);
        await InitializeDataAsync();
    }


    protected override void OnUnloaded()
    {
        CurrentSummary = null!;
        SelectMonthData = null;
        MonthDataList = null!;
        CurrentSeries = null;
        SelectSeries = null;
        DayDataList = null!;
    }


    private TrailblazeCalendarSummary currentSummary;

    public TrailblazeCalendarSummary CurrentSummary
    {
        get => currentSummary;
        set
        {
            if (EqualityComparer<TrailblazeCalendarSummary>.Default.Equals(currentSummary, value)) return;
            currentSummary = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSummary)));
        }
    }


    private TrailblazeCalendarMonthData? selectMonthData;

    public TrailblazeCalendarMonthData? SelectMonthData
    {
        get => selectMonthData;
        set
        {
            if (EqualityComparer<TrailblazeCalendarMonthData?>.Default.Equals(selectMonthData, value)) return;
            selectMonthData = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectMonthData)));
        }
    }


    private List<TrailblazeCalendarMonthData> monthDataList;

    public List<TrailblazeCalendarMonthData> MonthDataList
    {
        get => monthDataList;
        set
        {
            if (EqualityComparer<List<TrailblazeCalendarMonthData>>.Default.Equals(monthDataList, value)) return;
            monthDataList = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonthDataList)));
        }
    }


    private List<ChartLegend>? currentSeries;

    public List<ChartLegend>? CurrentSeries
    {
        get => currentSeries;
        set
        {
            if (EqualityComparer<List<ChartLegend>?>.Default.Equals(currentSeries, value)) return;
            currentSeries = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSeries)));
        }
    }


    private List<ChartLegend>? selectSeries;

    public List<ChartLegend>? SelectSeries
    {
        get => selectSeries;
        set
        {
            if (EqualityComparer<List<ChartLegend>?>.Default.Equals(selectSeries, value)) return;
            selectSeries = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectSeries)));
        }
    }


    private List<CalendarDayData> dayDataList;

    public List<CalendarDayData> DayDataList
    {
        get => dayDataList;
        set
        {
            if (EqualityComparer<List<CalendarDayData>>.Default.Equals(dayDataList, value)) return;
            dayDataList = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DayDataList)));
        }
    }


    private static readonly Dictionary<string, Color> actionColorMap = new Dictionary<string, Color>()
    {
        ["daily_reward"] = Color.FromArgb(0xFF, 0xFE, 0xC6, 0x6F),
        ["space_reward"] = Color.FromArgb(0xFF, 0x44, 0xDD, 0x9C),
        ["event_reward"] = Color.FromArgb(0xFF, 0x47, 0xC6, 0xFD),
        ["adventure_reward"] = Color.FromArgb(0xFF, 0x88, 0x7F, 0xFE),
        ["abyss_reward"] = Color.FromArgb(0xFF, 0xDF, 0x53, 0xFE),
        ["mail_reward"] = Color.FromArgb(0xFF, 0xF8, 0x4E, 0x35),
        ["other"] = Color.FromArgb(0xFF, 0xFD, 0xEA, 0x60),
    };


    [RelayCommand]
    private async Task InitializeDataAsync()
    {
        await Task.Delay(16);
        await GetCurrentSummaryAsync();
        GetMonthDataList();
    }


    private async Task GetCurrentSummaryAsync()
    {
        try
        {
            if (gameRole is null)
            {
                return;
            }
            CurrentSummary = await _gameRecordService.GetTrailblazeCalendarSummaryAsync(gameRole);
            MenuFlyout_GetDetails.Items.Clear();
            foreach (string monthStr in CurrentSummary.OptionalMonth)
            {
                if (DateTime.TryParseExact(monthStr, "yyyyMM", null, System.Globalization.DateTimeStyles.None, out DateTime time))
                {
                    MenuFlyout_GetDetails.Items.Add(new MenuItem
                    {
                        Header = time.ToString("MMM"),
                        Command = GetDataDetailsCommand,
                        CommandParameter = monthStr,
                    });
                }
                else
                {
                    MenuFlyout_GetDetails.Items.Add(new MenuItem
                    {
                        Header = monthStr,
                        Command = GetDataDetailsCommand,
                        CommandParameter = monthStr,
                    });
                }
            }
            CurrentSeries = CurrentSummary.MonthData.GroupBy.Select(x => new ChartLegend(x.ActionName, x.Percent, new SolidColorBrush(actionColorMap.GetValueOrDefault(x.Action)))).ToList();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Get realtime trailblaze calendar data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            LunarWard.Helpers.InAppToast.MainWindow?.Warning(Lang.Common_AccountError, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Get realtime trailblaze calendar data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            LunarWard.Helpers.InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get realtime trailblaze calendar data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            LunarWard.Helpers.InAppToast.MainWindow?.Error(ex);
        }
    }


    private void GetMonthDataList()
    {
        try
        {
            SelectMonthData = null;
            MonthDataList = _gameRecordService.GetTrailblazeCalendarMonthDataList(gameRole);
            Image_Emoji.IsVisible = MonthDataList.Any() ? false : true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load trailblaze calendar month data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    [RelayCommand]
    private async Task GetDataDetailsAsync(string month)
    {
        try
        {
            if (gameRole is null)
            {
                return;
            }
            await _gameRecordService.GetTrailblazeCalendarSummaryAsync(gameRole, month);
            await _gameRecordService.GetTrailblazeCalendarDetailAsync(gameRole, month, 1);
            await _gameRecordService.GetTrailblazeCalendarDetailAsync(gameRole, month, 2);
            GetMonthDataList();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Get trailblaze calendar data details ({gameBiz}, {uid}, {month}).", gameRole?.GameBiz, gameRole?.Uid, month);
            LunarWard.Helpers.InAppToast.MainWindow?.Warning(Lang.Common_AccountError, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Get trailblaze calendar data details ({gameBiz}, {uid}, {month}).", gameRole?.GameBiz, gameRole?.Uid, month);
            LunarWard.Helpers.InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get trailblaze calendar data details ({gameBiz}, {uid}, {month}).", gameRole?.GameBiz, gameRole?.Uid, month);
            LunarWard.Helpers.InAppToast.MainWindow?.Error(ex);
        }
    }


    private void ListView_MonthDataList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.AddedItems.Cast<object?>().FirstOrDefault() is TrailblazeCalendarMonthData data)
            {
                SelectMonthData = data;
                SelectSeries = SelectMonthData.GroupBy.Select(x => new ChartLegend(x.ActionName, x.Percent, new SolidColorBrush(actionColorMap.GetValueOrDefault(x.Action)))).ToList();
                RefreshDailyDataPlot(data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selection changed ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    private void RefreshDailyDataPlot(TrailblazeCalendarMonthData data)
    {
        try
        {
            var items_jade = _gameRecordService.GetTrailblazeCalendarDetailItems(data.Uid, data.Month, 1);
            var items_pass = _gameRecordService.GetTrailblazeCalendarDetailItems(data.Uid, data.Month, 2);
            int days = DateTime.DaysInMonth(int.Parse(data.Month[..4]), int.Parse(data.Month[4..]));
            var x = Enumerable.Range(1, days).ToArray();

            var stats_jade = new int[days];
            foreach (var item in items_jade)
            {
                var day = item.Time.Day;
                if (day <= days)
                {
                    stats_jade[day - 1] += item.Number;
                }
            }

            var stats_pass = new int[days];
            foreach (var item in items_pass)
            {
                var day = item.Time.Day;
                if (day <= days)
                {
                    stats_pass[day - 1] += item.Number;
                }
            }

            double max_jade = stats_jade.Max();
            double max_pass = stats_pass.Max();
            max_jade = max_jade == 0 ? double.MaxValue : max_jade;
            max_pass = max_pass == 0 ? double.MaxValue : max_pass;
            var list = new List<CalendarDayData>(days);
            for (int i = 0; i < days; i++)
            {
                list.Add(new CalendarDayData
                {
                    Day = $"{data.Month[4..]}-{i + 1:D2}",
                    Jade = stats_jade[i],
                    Pass = stats_pass[i],
                    JadeProgress = stats_jade[i] / max_jade,
                    PassProgress = stats_pass[i] / max_pass,
                });
            }
            DayDataList = list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh daily data plot");
        }
    }


    public class CalendarDayData
    {

        public string Day { get; set; }

        public int Jade { get; set; }

        public int Pass { get; set; }

        public double JadeProgress { get; set; }

        public double PassProgress { get; set; }

    }


}
