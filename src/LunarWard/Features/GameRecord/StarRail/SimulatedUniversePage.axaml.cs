using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.StarRail.SimulatedUniverse;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.ComponentModel;


namespace LunarWard.Features.GameRecord.StarRail;

public sealed partial class SimulatedUniversePage : PageBase
{


    private readonly ILogger<SimulatedUniversePage> _logger = AppConfig.GetLogger<SimulatedUniversePage>();

    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    public SimulatedUniversePage()
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
        InitializeSimulatedUniverseRecord();
    }


    protected override void OnUnloaded()
    {
        BasicInfo = null!;
        CurrentRecord = null;
        RecordBasicList = null!;
    }


    private SimulatedUniverseBasicStats basicInfo;

    public SimulatedUniverseBasicStats BasicInfo
    {
        get => basicInfo;
        set
        {
            if (EqualityComparer<SimulatedUniverseBasicStats>.Default.Equals(basicInfo, value)) return;
            basicInfo = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BasicInfo)));
        }
    }


    private SimulatedUniverseRecord? currentRecord;

    public SimulatedUniverseRecord? CurrentRecord
    {
        get => currentRecord;
        set
        {
            if (EqualityComparer<SimulatedUniverseRecord?>.Default.Equals(currentRecord, value)) return;
            currentRecord = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentRecord)));
        }
    }


    private List<SimulatedUniverseRecordBasic> recordBasicList;

    public List<SimulatedUniverseRecordBasic> RecordBasicList
    {
        get => recordBasicList;
        set
        {
            if (EqualityComparer<List<SimulatedUniverseRecordBasic>>.Default.Equals(recordBasicList, value)) return;
            recordBasicList = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RecordBasicList)));
        }
    }


    private void InitializeSimulatedUniverseRecord()
    {
        try
        {
            if (gameRole is null)
            {
                Image_Emoji.IsVisible = true;
                return;
            }
            CurrentRecord = null;
            var list = _gameRecordService.GetSimulatedUniverseRecordBasics(gameRole);
            if (list.Count != 0)
            {
                RecordBasicList = list;
                ListView_SimulatedUniverse.SelectedIndex = 0;
            }
            else
            {
                Image_Emoji.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Init simulated universe record basic data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    [RelayCommand]
    private async Task GetSimulatedUniverseDetailAsync()
    {
        await GetSimulatedUniverseInfoBasicAsync(true);
        InitializeSimulatedUniverseRecord();
    }


    private async Task GetSimulatedUniverseInfoBasicAsync(bool detail = false)
    {
        try
        {
            if (gameRole is null)
            {
                return;
            }
            var info = await _gameRecordService.GetSimulatedUniverseInfoAsync(gameRole, detail);
            BasicInfo = info.BasicInfo;
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Get simulated universe data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            GameRecordPage.HandleMiHoYoApiException(ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Get simulated universe data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            LunarWard.Helpers.InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get simulated universe data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            LunarWard.Helpers.InAppToast.MainWindow?.Error(ex);
        }
    }


    private void ListView_SimulatedUniverse_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Cast<object?>().FirstOrDefault() is SimulatedUniverseRecordBasic data)
        {
            GetSimulatedUniverseRecord(data);
        }
    }


    private void GetSimulatedUniverseRecord(SimulatedUniverseRecordBasic data)
    {
        try
        {
            if (gameRole is null)
            {
                return;
            }
            CurrentRecord = _gameRecordService.GetSimulatedUniverseRecord(gameRole, data.ScheduleId);
            Image_Emoji.IsVisible = (CurrentRecord?.HasData ?? false) ? false : true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get stimulated record (Uid {uid}, ScheduleId {scheduleId})", gameRole.Uid, data.ScheduleId);
        }
    }


}
