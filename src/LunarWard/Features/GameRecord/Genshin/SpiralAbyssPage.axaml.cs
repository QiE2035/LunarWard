using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.Genshin.SpiralAbyss;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.ComponentModel;


namespace LunarWard.Features.GameRecord.Genshin;

public sealed partial class SpiralAbyssPage : PageBase
{


    private readonly ILogger<SpiralAbyssPage> _logger = AppConfig.GetLogger<SpiralAbyssPage>();

    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    public SpiralAbyssPage()
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
        await Task.Delay(160);
        InitializeAbyssData();
    }


    protected override void OnUnloaded()
    {
        CurrentAbyss = null;
        AbyssList = null!;
    }


    private bool hasData;

    public bool HasData
    {
        get => hasData;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(hasData, value)) return;
            hasData = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasData)));
        }
    }


    private List<SpiralAbyssInfo> abyssList;

    public List<SpiralAbyssInfo> AbyssList
    {
        get => abyssList;
        set
        {
            if (EqualityComparer<List<SpiralAbyssInfo>>.Default.Equals(abyssList, value)) return;
            abyssList = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AbyssList)));
        }
    }


    private SpiralAbyssInfo? currentAbyss;

    public SpiralAbyssInfo? CurrentAbyss
    {
        get => currentAbyss;
        set
        {
            if (EqualityComparer<SpiralAbyssInfo?>.Default.Equals(currentAbyss, value)) return;
            currentAbyss = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentAbyss)));
        }
    }


    public List<int> RevealRankInternalStar => Enumerable.Range(0, Math.Clamp((CurrentAbyss?.RevealRank?.Count ?? 1) - 1, 0, int.MaxValue)).ToList();


    private void InitializeAbyssData()
    {
        try
        {
            CurrentAbyss = null;
            var list = _gameRecordService.GetSpiralAbyssInfoList(gameRole);
            if (list.Any())
            {
                AbyssList = list;
                ListView_AbyssList.SelectedIndex = 0;
            }
            else
            {
                Image_Emoji.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Init abyss data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
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
            await _gameRecordService.RefreshSpiralAbyssInfoAsync(gameRole, 1);
            await _gameRecordService.RefreshSpiralAbyssInfoAsync(gameRole, 2);
            InitializeAbyssData();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Refresh abyss data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            GameRecordPage.HandleMiHoYoApiException(ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Refresh abyss data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            LunarWard.Helpers.InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh abyss data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            LunarWard.Helpers.InAppToast.MainWindow?.Error(ex);
        }
    }


    private void ListView_AbyssList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.AddedItems.Cast<object?>().FirstOrDefault() is SpiralAbyssInfo info)
            {
                CurrentAbyss = _gameRecordService.GetSpiralAbyssInfo(gameRole, info.ScheduleId);
                HasData = CurrentAbyss?.TotalBattleCount > 0;
                Image_Emoji.IsVisible = HasData ? false : true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selection changed ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    private void UserControl_AbyssLevel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Linux: 上游用 VisualStateManager.GoToState("WideState"/"NarrowState") 切深渊楼层布局；
        // Avalonia 没有 VisualStateManager，这里直接按可用宽度改样式类（样式里定义同一组尺寸）。
        bool wide = e.NewSize.Width > 720;
        if (sender is Control control)
        {
            control.Classes.Set("wide", wide);
            control.Classes.Set("narrow", !wide);
        }
    }


    public static string FloorX(int x)
    {
        return Lang.SpiralAbyssPage_FloorX.Replace("{X}", x.ToString());
    }


    public static string LevelX(int x)
    {
        return Lang.SpiralAbyssPage_ChamberX.Replace("{X}", x.ToString());
    }


}
