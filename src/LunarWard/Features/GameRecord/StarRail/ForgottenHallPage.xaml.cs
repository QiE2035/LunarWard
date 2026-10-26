using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.StarRail.ForgottenHall;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.ComponentModel;


namespace LunarWard.Features.GameRecord.StarRail;

public sealed partial class ForgottenHallPage : PageBase
{


    private readonly ILogger<ForgottenHallPage> _logger = AppConfig.GetLogger<ForgottenHallPage>();

    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    public ForgottenHallPage()
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
        InitializeForgottenHallData();
    }


    protected override void OnUnloaded()
    {
        CurrentForgottenHall = null;
        ForgottenHallList = null!;
    }


    private List<ForgottenHallInfo> forgottenHallList;

    public List<ForgottenHallInfo> ForgottenHallList
    {
        get => forgottenHallList;
        set
        {
            if (EqualityComparer<List<ForgottenHallInfo>>.Default.Equals(forgottenHallList, value)) return;
            forgottenHallList = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ForgottenHallList)));
        }
    }


    private ForgottenHallInfo? currentForgottenHall;

    public ForgottenHallInfo? CurrentForgottenHall
    {
        get => currentForgottenHall;
        set
        {
            if (EqualityComparer<ForgottenHallInfo?>.Default.Equals(currentForgottenHall, value)) return;
            currentForgottenHall = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentForgottenHall)));
        }
    }


    private void InitializeForgottenHallData()
    {
        try
        {
            CurrentForgottenHall = null;
            var list = _gameRecordService.GetForgottenHallInfoList(gameRole);
            if (list.Count != 0)
            {
                ForgottenHallList = list;
                ListView_ForgottenHall.SelectedIndex = 0;
            }
            else
            {
                Image_Emoji.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Init forgotten hall data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
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
            await _gameRecordService.RefreshForgottenHallInfoAsync(gameRole, 1);
            await _gameRecordService.RefreshForgottenHallInfoAsync(gameRole, 2);
            InitializeForgottenHallData();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Refresh forgotten hall data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            GameRecordPage.HandleMiHoYoApiException(ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Refresh forgotten hall data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh forgotten hall data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Error(ex);
        }
    }


    private void ListView_ForgottenHall_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.AddedItems.FirstOrDefault() is ForgottenHallInfo info)
            {
                CurrentForgottenHall = _gameRecordService.GetForgottenHallInfo(gameRole, info.ScheduleId);
                Image_Emoji.IsVisible = (CurrentForgottenHall?.HasData ?? false) ? false : true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selection changed ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    public static bool FloorHasExtraStar(int starNum) => starNum > 3;


}
