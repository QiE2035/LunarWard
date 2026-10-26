using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.StarRail.PureFiction;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.ComponentModel;


namespace LunarWard.Features.GameRecord.StarRail;

public sealed partial class PureFictionPage : PageBase
{


    private readonly ILogger<PureFictionPage> _logger = AppConfig.GetLogger<PureFictionPage>();

    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    public PureFictionPage()
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
        InitializePureFictionInfoData();
    }


    protected override void OnUnloaded()
    {
        CurrentPureFiction = null;
        PureFictionList = null!;
    }


    private List<PureFictionInfo> pureFictionList;

    public List<PureFictionInfo> PureFictionList
    {
        get => pureFictionList;
        set
        {
            if (EqualityComparer<List<PureFictionInfo>>.Default.Equals(pureFictionList, value)) return;
            pureFictionList = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PureFictionList)));
        }
    }


    private PureFictionInfo? currentPureFiction;

    public PureFictionInfo? CurrentPureFiction
    {
        get => currentPureFiction;
        set
        {
            if (EqualityComparer<PureFictionInfo?>.Default.Equals(currentPureFiction, value)) return;
            currentPureFiction = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPureFiction)));
        }
    }


    private void InitializePureFictionInfoData()
    {
        try
        {
            CurrentPureFiction = null;
            var list = _gameRecordService.GetPureFictionInfoList(gameRole);
            if (list.Count != 0)
            {
                PureFictionList = list;
                ListView_ForgottenHall.SelectedIndex = 0;
            }
            else
            {
                Image_Emoji.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Init pure fiction data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
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
            await _gameRecordService.RefreshPureFictionInfoAsync(gameRole, 1);
            await _gameRecordService.RefreshPureFictionInfoAsync(gameRole, 2);
            InitializePureFictionInfoData();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Refresh pure fiction data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            GameRecordPage.HandleMiHoYoApiException(ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Refresh pure fiction data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            LunarWard.Helpers.InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh pure fiction data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            LunarWard.Helpers.InAppToast.MainWindow?.Error(ex);
        }
    }


    private void ListView_ForgottenHall_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.AddedItems.Cast<object?>().FirstOrDefault() is PureFictionInfo info)
            {
                CurrentPureFiction = _gameRecordService.GetPureFictionInfo(gameRole, info.ScheduleId);
                Image_Emoji.IsVisible = (CurrentPureFiction?.HasData ?? false) ? false : true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selection changed ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    public static bool FloorHasExtraStar(int starNum) => starNum > 3;


}
