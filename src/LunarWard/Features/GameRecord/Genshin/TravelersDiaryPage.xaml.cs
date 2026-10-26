using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LunarWard.Controls;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.Genshin.TravelersDiary;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media;
using System.ComponentModel;


namespace LunarWard.Features.GameRecord.Genshin;

public sealed partial class TravelersDiaryPage : PageBase
{


    private readonly ILogger<TravelersDiaryPage> _logger = AppConfig.GetLogger<TravelersDiaryPage>();


    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    public TravelersDiaryPage()
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


    private TravelersDiarySummary currentSummary;

    public TravelersDiarySummary CurrentSummary
    {
        get => currentSummary;
        set
        {
            if (EqualityComparer<TravelersDiarySummary>.Default.Equals(currentSummary, value)) return;
            currentSummary = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentSummary)));
        }
    }


    private TravelersDiaryMonthData? selectMonthData;

    public TravelersDiaryMonthData? SelectMonthData
    {
        get => selectMonthData;
        set
        {
            if (EqualityComparer<TravelersDiaryMonthData?>.Default.Equals(selectMonthData, value)) return;
            selectMonthData = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectMonthData)));
        }
    }


    private List<TravelersDiaryMonthData> monthDataList;

    public List<TravelersDiaryMonthData> MonthDataList
    {
        get => monthDataList;
        set
        {
            if (EqualityComparer<List<TravelersDiaryMonthData>>.Default.Equals(monthDataList, value)) return;
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


    private List<DiaryDayData> dayDataList;

    public List<DiaryDayData> DayDataList
    {
        get => dayDataList;
        set
        {
            if (EqualityComparer<List<DiaryDayData>>.Default.Equals(dayDataList, value)) return;
            dayDataList = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DayDataList)));
        }
    }


    private static readonly Dictionary<int, Color> actionColorMap = new Dictionary<int, Color>()
    {
        [0] = Color.FromArgb(0xFF, 0x72, 0xA7, 0xC6),
        [1] = Color.FromArgb(0xFF, 0xD4, 0x64, 0x63),
        [2] = Color.FromArgb(0xFF, 0x6F, 0xB0, 0xB2),
        [3] = Color.FromArgb(0xFF, 0xBC, 0x99, 0x59),
        [4] = Color.FromArgb(0xFF, 0x72, 0x98, 0x6F),
        [5] = Color.FromArgb(0xFF, 0x79, 0x6B, 0xA6),
        [6] = Color.FromArgb(0xFF, 0x59, 0x7D, 0x9F),
        [7] = Color.FromArgb(0xFF, 0x7A, 0x7C, 0xB2),
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
            CurrentSummary = await _gameRecordService.GetTravelersDiarySummaryAsync(gameRole);
            MenuFlyout_GetDetails.Items.Clear();
            foreach (int month in Enumerable.Reverse(CurrentSummary.OptionalMonth))
            {
                MenuFlyout_GetDetails.Items.Add(new MenuFlyoutItem
                {
                    Text = new DateTime(2023, month, 1).ToString("MMM"),
                    Command = GetDataDetailsCommand,
                    CommandParameter = month,
                });
            }
            CurrentSeries = CurrentSummary.MonthData.PrimogemsGroupBy.Select(x => new ChartLegend(x.ActionName, x.Percent, actionColorMap.GetValueOrDefault(x.ActionId))).ToList();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Get realtime traveler's diary data details ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Warning(Lang.Common_AccountError, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Get realtime traveler's diary data details ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get realtime traveler's diary data details ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Error(ex);
        }
    }


    private void GetMonthDataList()
    {
        try
        {
            SelectMonthData = null;
            MonthDataList = _gameRecordService.GetTravelersDiaryMonthDataList(gameRole);
            Image_Emoji.IsVisible = MonthDataList.Any() ? false : true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load traveler's diary month data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    [RelayCommand]
    private async Task GetDataDetailsAsync(int month)
    {
        try
        {
            if (gameRole is null)
            {
                return;
            }
            await _gameRecordService.GetTravelersDiarySummaryAsync(gameRole, month);
            await _gameRecordService.GetTravelersDiaryDetailAsync(gameRole, month, 1);
            await _gameRecordService.GetTravelersDiaryDetailAsync(gameRole, month, 2);
            GetMonthDataList();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Get traveler's diary data details ({gameBiz}, {uid}, {month}).", gameRole?.GameBiz, gameRole?.Uid, month);
            InAppToast.MainWindow?.Warning(Lang.Common_AccountError, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Get traveler's diary data details ({gameBiz}, {uid}, {month}).", gameRole?.GameBiz, gameRole?.Uid, month);
            InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get traveler's diary data details ({gameBiz}, {uid}, {month}).", gameRole?.GameBiz, gameRole?.Uid, month);
            InAppToast.MainWindow?.Error(ex);
        }
    }


    private void ListView_MonthDataList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.AddedItems.FirstOrDefault() is TravelersDiaryMonthData data)
            {
                SelectMonthData = data;
                SelectSeries = SelectMonthData.PrimogemsGroupBy.Select(x => new ChartLegend(x.ActionName, x.Percent, actionColorMap.GetValueOrDefault(x.ActionId))).ToList();
                RefreshDailyDataPlot(data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selection changed ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    private void RefreshDailyDataPlot(TravelersDiaryMonthData data)
    {
        try
        {
            var items_primogems = _gameRecordService.GetTravelersDiaryDetailItems(data.Uid, data.Year, data.Month, 1);
            var items_mora = _gameRecordService.GetTravelersDiaryDetailItems(data.Uid, data.Year, data.Month, 2);
            int days = DateTime.DaysInMonth(data.Year, data.Month);
            var x = Enumerable.Range(1, days).ToArray();

            var stats_primogems = new int[days];
            foreach (var item in items_primogems)
            {
                var day = item.Time.Day;
                if (day <= days)
                {
                    stats_primogems[day - 1] += item.Number;
                }
            }

            var stats_mora = new int[days];
            foreach (var item in items_mora)
            {
                var day = item.Time.Day;
                if (day <= days)
                {
                    stats_mora[day - 1] += item.Number;
                }
            }

            double max_primogems = stats_primogems.Max();
            double max_mora = stats_mora.Max();
            max_primogems = max_primogems == 0 ? double.MaxValue : max_primogems;
            max_mora = max_mora == 0 ? double.MaxValue : max_mora;
            var list = new List<DiaryDayData>(days);
            for (int i = 0; i < days; i++)
            {
                list.Add(new DiaryDayData
                {
                    Day = $"{data.Month:D2}-{i + 1:D2}",
                    Primogems = stats_primogems[i],
                    Mora = stats_mora[i],
                    PrimogemsProgress = stats_primogems[i] / max_primogems,
                    MoraProgress = stats_mora[i] / max_mora,
                });
            }
            DayDataList = list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh daily data plot");
        }
    }


    public class DiaryDayData
    {

        public string Day { get; set; }

        public int Primogems { get; set; }

        public int Mora { get; set; }

        public double PrimogemsProgress { get; set; }

        public double MoraProgress { get; set; }

    }


}
