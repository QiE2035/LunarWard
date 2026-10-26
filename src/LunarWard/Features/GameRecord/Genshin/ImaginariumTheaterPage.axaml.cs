using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.Genshin.ImaginariumTheater;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Globalization;


namespace LunarWard.Features.GameRecord.Genshin;

public sealed partial class ImaginariumTheaterPage : PageBase
{

    private readonly ILogger<ImaginariumTheaterPage> _logger = AppConfig.GetLogger<ImaginariumTheaterPage>();

    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    public ImaginariumTheaterPage()
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
        InitializeTheaterData();
    }


    protected override void OnUnloaded()
    {
        CurrentTheater = null;
        TheaterList = null!;
    }


    public bool HasData { get; set => SetProperty(ref field, value); }


    public List<ImaginariumTheaterInfo> TheaterList { get; set => SetProperty(ref field, value); }


    public ImaginariumTheaterInfo? CurrentTheater { get; set => SetProperty(ref field, value); }


    private void InitializeTheaterData()
    {
        try
        {
            CurrentTheater = null;
            var list = _gameRecordService.GetImaginariumTheaterInfoList(gameRole);
            if (list.Count != 0)
            {
                TheaterList = list;
                ListView_TheaterList.SelectedIndex = 0;
            }
            else
            {
                Image_Emoji.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Init theater data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
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
            await _gameRecordService.RefreshImaginariumTheaterInfoAsync(gameRole);
            InitializeTheaterData();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Refresh theater data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            GameRecordPage.HandleMiHoYoApiException(ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Refresh theater data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            LunarWard.Helpers.InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh theater data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            LunarWard.Helpers.InAppToast.MainWindow?.Error(ex);
        }
    }


    private void ListView_TheaterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.AddedItems.Cast<object?>().FirstOrDefault() is ImaginariumTheaterInfo info)
            {
                CurrentTheater = _gameRecordService.GetImaginariumTheaterInfo(gameRole, info.ScheduleId);
                HasData = CurrentTheater?.HasData ?? false;
                Image_Emoji.IsVisible = HasData ? false : true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selection changed ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    public static string DifficultyMode(int difficult)
    {
        return difficult switch
        {
            1 => Lang.ImaginariumTheaterPage_EasyMode,
            2 => Lang.ImaginariumTheaterPage_NormalMode,
            3 => Lang.ImaginariumTheaterPage_HardMode,
            4 => Lang.ImaginariumTheaterPage_VisionaryMode,
            5 => Lang.ImaginariumTheaterPage_LunarMode,
            _ => "",
        };
    }


    public static bool LunarModeVisibility(int difficult)
    {
        return difficult == 5 ? true : false;
    }


    public static string ActX(int x)
    {
        return string.Format(Lang.ImaginariumTheaterPage_Act0, x);
    }


    public static string ArcanaChallengeX(int x)
    {
        return string.Format(Lang.ImaginariumTheaterPage_ArcanaChallenge0, x);
    }


    public static bool StarIconVisibility(int value)
    {
        return value == 0 ? false : true;
    }


    public static string PerformancesTime(int second)
    {
        var ts = TimeSpan.FromSeconds(second);
        return $"{ts.Minutes}m {ts.Seconds}s";
    }


    public static bool FightStatisicVisibility(int value)
    {
        return value == 0 ? false : true;
    }


    public static Bitmap? HeraldryImage(int heraldry)
    {
        return heraldry switch
        {
            0 => GameRecordAssets.FromMsAppx("Assets/Image/UI_RoleCombat_Medal_0.png"),
            1 => GameRecordAssets.FromMsAppx("Assets/Image/UI_RoleCombat_Medal_1.png"),
            2 => GameRecordAssets.FromMsAppx("Assets/Image/UI_RoleCombat_Medal_2.png"),
            3 => GameRecordAssets.FromMsAppx("Assets/Image/UI_RoleCombat_Medal_3.png"),
            4 => GameRecordAssets.FromMsAppx("Assets/Image/UI_RoleCombat_Medal_4.png"),
            5 => GameRecordAssets.FromMsAppx("Assets/Image/UI_RoleCombat_Medal_5.png"),
            _ => null,
        };
    }


    public static string DifficultyIdToMaxRound(int difficulty)
    {
        return difficulty switch
        {
            1 => "3",
            2 => "6",
            3 => "8",
            4 => "10",
            5 => "12",
            _ => "-",
        };
    }


    public static bool IsBrilliantBlessingVisibility(int? level)
    {
        return level switch
        {
            null or 0 => false,
            _ => true,
        };
    }


    public static double BrilliantBlessingBuffOpacity(int level)
    {
        return level switch
        {
            0 => 0.3,
            _ => 1,
        };
    }


}


public partial class TheaterStarIconVisibilityConverter : IValueConverter
{

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is 0 ? false : true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


