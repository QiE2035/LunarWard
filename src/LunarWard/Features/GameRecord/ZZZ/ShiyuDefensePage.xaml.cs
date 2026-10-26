using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.ZZZ.ShiyuDefense;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;


namespace LunarWard.Features.GameRecord.ZZZ;

public sealed partial class ShiyuDefensePage : PageBase
{


    private readonly ILogger<ShiyuDefensePage> _logger = AppConfig.GetLogger<ShiyuDefensePage>();

    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    public ShiyuDefensePage()
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
        InitializeShiyuDefenseInfoData();
    }


    protected override void OnUnloaded()
    {
        CurrentShiyuDefense = null;
        CurrentShiyuDefenseV2 = null;
        ShiyuDefenseList = null!;
    }


    public List<ShiyuDefenseInfo> ShiyuDefenseList { get; set => SetProperty(ref field, value); }


    public ShiyuDefenseInfo? CurrentShiyuDefense { get; set => SetProperty(ref field, value); }


    public ShiyuDefenseInfoV2? CurrentShiyuDefenseV2 { get; set => SetProperty(ref field, value); }


    private void InitializeShiyuDefenseInfoData()
    {
        try
        {
            CurrentShiyuDefense = null;
            CurrentShiyuDefenseV2 = null;
            var list = _gameRecordService.GetShiyuDefenseInfoList(gameRole);
            if (list.Count != 0)
            {
                ShiyuDefenseList = list;
                ListView_ShiyuDefense.SelectedIndex = 0;
            }
            else
            {
                Image_Emoji.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Init shiyu defense data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
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
            await _gameRecordService.RefreshShiyuDefenseInfoAsync(gameRole, 1);
            await _gameRecordService.RefreshShiyuDefenseInfoAsync(gameRole, 2);
            InitializeShiyuDefenseInfoData();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Refresh shiyu defense data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            GameRecordPage.HandleMiHoYoApiException(ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Refresh shiyu defense data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh shiyu defense data ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Error(ex);
        }
    }


    private void ListView_ShiyuDefense_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.AddedItems.FirstOrDefault() is ShiyuDefenseInfo info)
            {
                CurrentShiyuDefense = null;
                CurrentShiyuDefenseV2 = null;
                var detail = _gameRecordService.GetShiyuDefenseInfo(gameRole, info.ScheduleId);
                if (detail is ShiyuDefenseInfo infoV1)
                {
                    CurrentShiyuDefense = infoV1;
                }
                else if (detail is ShiyuDefenseInfoV2 infov2)
                {
                    CurrentShiyuDefenseV2 = infov2;
                }
                Image_Emoji.IsVisible = (info?.HasData ?? false) ? false : true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selection changed ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    public static string PerformancesTime(int second)
    {
        var ts = TimeSpan.FromSeconds(second);
        return $"{ts.Minutes}m {ts.Seconds:D2}s";
    }


    public static bool ElementWeaknessVisibility(int value, int weakOrResist)
    {
        return value == weakOrResist ? true : false;
    }


    public static bool V1VersionVisibility(string value)
    {
        return value is "v1" ? true : false;
    }


    public static bool V2VersionVisibility(string value)
    {
        return value is "v2" ? true : false;
    }


    public static string RankPercentText(int value)
    {
        int d = value / 100;
        int p = value % 100;
        return $"{d}.{p:D2}%";
    }


    /// <summary>
    /// 排名百分比背景图片，value 为以 0.01% 为单位的排名
    /// </summary>
    public static Bitmap RankBackground(int value)
    {
        var img = value switch
        {
            <= 100 => "ms-appx:///Assets/Image/rank-bg-1.6a51d893.png",
            <= 200 => "ms-appx:///Assets/Image/rank-bg-2.7522d7bb.png",
            <= 500 => "ms-appx:///Assets/Image/rank-bg-3.370b7f26.png",
            <= 3000 => "ms-appx:///Assets/Image/rank-bg-4.1b293bb1.png",
            _ => "ms-appx:///Assets/Image/rank-bg-5.cf630a83.png",
        };
        return new Bitmap(new Uri(img));
    }


    public static bool ChallengeTimeVisibility(DateTime dateTime)
    {
        return dateTime == DateTime.MinValue ? false : true;
    }


    public string GetFourthLayerChallengeTime(ShiyuDefenseInfoV2 info)
    {
        if (info is null)
        {
            return string.Empty;
        }
        var time = info.Brief.ChallengeTime;
        if (time == DateTime.MinValue)
        {
            time = info.FourthLayerDetail.ChallengeTime;
        }
        return time.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
