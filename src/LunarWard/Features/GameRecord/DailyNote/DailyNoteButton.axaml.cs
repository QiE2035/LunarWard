using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.BH3.DailyNote;
using LunarWard.Core.GameRecord.Genshin.DailyNote;
using LunarWard.Core.GameRecord.StarRail.DailyNote;
using LunarWard.Core.GameRecord.ZZZ.DailyNote;
using LunarWard.Core.HoYoPlay;
using System;
using System.Threading.Tasks;


namespace LunarWard.Features.GameRecord.DailyNote;
public sealed partial class DailyNoteButton : UserControl
{

    private readonly ILogger<DailyNoteButton> _logger = AppConfig.GetLogger<DailyNoteButton>();


    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    public DailyNoteButton()
    {
        this.InitializeComponent();
        this.IsVisible = false;
    }


    public static readonly StyledProperty<GameId?> CurrentGameIdProperty =
        AvaloniaProperty.Register<DailyNoteButton, GameId?>(nameof(CurrentGameId));

    public GameId? CurrentGameId
    {
        get => GetValue(CurrentGameIdProperty);
        set => SetValue(CurrentGameIdProperty, value);
    }

    static DailyNoteButton()
    {
        CurrentGameIdProperty.Changed.AddClassHandler<DailyNoteButton>((sender, _) =>
        {
            sender.OnPropertyChanged(nameof(IsBH3Enabled));
            sender.OnPropertyChanged(nameof(IsGenshinEnabled));
            sender.OnPropertyChanged(nameof(IsStarRailEnabled));
            sender.OnPropertyChanged(nameof(IsZZZEnabled));
        });
    }


    public bool IsBH3Enabled => CurrentGameId?.GameBiz.Game is GameBiz.bh3;

    public bool IsGenshinEnabled => CurrentGameId?.GameBiz.Game is GameBiz.hk4e;

    public bool IsStarRailEnabled => CurrentGameId?.GameBiz.Game is GameBiz.hkrpg;

    public bool IsZZZEnabled => CurrentGameId?.GameBiz.Game is GameBiz.nap;


    private GameRecordRole? GameRecordRole { get; set => SetProperty(ref field, value); }


    public BH3DailyNote BH3DailyNote { get; set => SetProperty(ref field, value); }

    public GenshinDailyNote GenshinDailyNote { get; set => SetProperty(ref field, value); }

    public StarRailDailyNote StarRailDailyNote { get; set => SetProperty(ref field, value); }

    public ZZZDailyNote ZZZDailyNote { get; set => SetProperty(ref field, value); }


    public string? ErrorMessage { get; set => SetProperty(ref field, value); }


    private void Button_DailyNote_Loaded(object sender, RoutedEventArgs e)
    {
        if (CurrentGameId is null)
        {
            return;
        }
        if (!GameFeatureConfig.FromGameId(CurrentGameId).SupportDailyNote)
        {
            return;
        }
        RefreshDailyNoteCommand.Execute(false);
    }


    private void Button_DailyNote_Unloaded(object sender, RoutedEventArgs e)
    {
        GameRecordRole = null;
        BH3DailyNote = null!;
        GenshinDailyNote = null!;
        StarRailDailyNote = null!;
        ZZZDailyNote = null!;
    }


    [RelayCommand]
    private async Task RefreshDailyNoteAsync(bool? forceUpdate)
    {
        try
        {
            if (!forceUpdate.HasValue)
            {
                forceUpdate = true;
            }
            GameBiz gameBiz = CurrentGameId.GameBiz;
            if (gameBiz.Server is "bilibili")
            {
                gameBiz = $"{gameBiz.Game}_cn";
            }
            _gameRecordService.IsHoyolab = gameBiz.Server is "global";
            GameRecordRole = _gameRecordService.GetLastSelectGameRecordRoleOrTheFirstOne(gameBiz);
            if (GameRecordRole is not null)
            {
                this.IsVisible = true;
                ErrorMessage = null;
                await _gameRecordService.UpdateDeviceFpAsync();
                if (IsBH3Enabled)
                {
                    BH3DailyNote = await _gameRecordService.GetBH3DailyNoteAsync(GameRecordRole, forceUpdate.Value);
                }
                if (IsGenshinEnabled)
                {
                    GenshinDailyNote = await _gameRecordService.GetGenshinDailyNoteAsync(GameRecordRole, forceUpdate.Value);
                }
                if (IsStarRailEnabled)
                {
                    StarRailDailyNote = await _gameRecordService.GetStarRailDailyNoteAsync(GameRecordRole, forceUpdate.Value);
                }
                if (IsZZZEnabled)
                {
                    ZZZDailyNote = await _gameRecordService.GetZZZDailyNoteAsync(GameRecordRole, forceUpdate.Value);
                }
            }
        }
        catch (Exception ex)
        {
            if (ex is miHoYoApiException)
            {
                ErrorMessage = $"Error Code: {ex.Message}";
            }
            _logger.LogError(ex, "Refresh daily note failed (Biz: {GameBiz}, Server: {GameServer}, Uid: {Uid})", CurrentGameId?.GameBiz, GameRecordRole?.Region, GameRecordRole?.Uid);
        }
    }


    public static string ZZZMemberCardRemainingDaysToString(int remainingDays)
    {
        return string.Format(Lang.DailyNoteButton_0DaySRemaining, remainingDays);
    }


}
