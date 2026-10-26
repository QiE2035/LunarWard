using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.StarRail.ApocalypticShadow;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.ComponentModel;


namespace LunarWard.Features.GameRecord.StarRail;

public sealed partial class ApocalypticShadowPage : PageBase
{


    private readonly ILogger<ApocalypticShadowPage> _logger = AppConfig.GetLogger<ApocalypticShadowPage>();

    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    public ApocalypticShadowPage()
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
        InitializeApocalypticShadowInfoData();
    }


    protected override void OnUnloaded()
    {
        CurrentApocalypticShadow = null;
        ApocalypticShadowList = null!;
    }


    private List<ApocalypticShadowInfo> apocalypticShadowList;

    public List<ApocalypticShadowInfo> ApocalypticShadowList
    {
        get => apocalypticShadowList;
        set
        {
            if (EqualityComparer<List<ApocalypticShadowInfo>>.Default.Equals(apocalypticShadowList, value)) return;
            apocalypticShadowList = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ApocalypticShadowList)));
        }
    }


    private ApocalypticShadowInfo? currentApocalypticShadow;

    public ApocalypticShadowInfo? CurrentApocalypticShadow
    {
        get => currentApocalypticShadow;
        set
        {
            if (EqualityComparer<ApocalypticShadowInfo?>.Default.Equals(currentApocalypticShadow, value)) return;
            currentApocalypticShadow = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentApocalypticShadow)));
            OnCurrentApocalypticShadowChanged(value);
        }
    }


    private void InitializeApocalypticShadowInfoData()
    {
        try
        {
            CurrentApocalypticShadow = null;
            var list = _gameRecordService.GetApocalypticShadowInfoList(gameRole);
            if (list.Count != 0)
            {
                ApocalypticShadowList = list;
                ListView_ForgottenHall.SelectedIndex = 0;
            }
            else
            {
                Image_Emoji.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Init apocalyptic shadow data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        try
        {
            if (gameRole is null)
            {
                return;
            }
            await _gameRecordService.RefreshApocalypticShadowInfoAsync(gameRole, 1);
            await _gameRecordService.RefreshApocalypticShadowInfoAsync(gameRole, 2);
            InitializeApocalypticShadowInfoData();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Refresh apocalyptic shadow data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            GameRecordPage.HandleMiHoYoApiException(ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Refresh apocalyptic shadow data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh apocalyptic shadow data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Error(ex);
        }
    }


    private void ListView_ForgottenHall_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.AddedItems.FirstOrDefault() is ApocalypticShadowInfo info)
            {
                CurrentApocalypticShadow = _gameRecordService.GetApocalypticShadowInfo(gameRole, info.ScheduleId);
                Image_Emoji.IsVisible = (CurrentApocalypticShadow?.HasData ?? false) ? false : true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selection changed ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    public static bool FloorHasExtraStar(int starNum) => starNum > 3;


    private void OnCurrentApocalypticShadowChanged(ApocalypticShadowInfo? value)
    {
        if (BossPanelGrid is null)
        {
            return;
        }
        if (value?.Meta?.TierceBoss != null)
        {
            BossPanelGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            BossPanelGrid.ColumnDefinitions[2].Width = new GridLength(0);
        }
    }


}
