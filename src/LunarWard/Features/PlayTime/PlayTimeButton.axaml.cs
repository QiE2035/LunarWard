using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LunarWard.Core;
using LunarWard.Features.Database;
using LunarWard.Services;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LunarWard.Features.PlayTime;

/// <summary>
/// 游戏时长按钮：悬浮在游戏启动页/主窗口上，显示累计时长，点击打开 <see cref="PlayTimeStatsDialog"/>。
/// </summary>
/// <remarks>
/// 对应上游 <c>Starward.Features.PlayTime.PlayTimeButton</c>。平台差异（docs/09 十六）：
/// <c>FontIcon</c>→<c>TextBlock</c> + <c>StarIconFontFamily</c>；
/// <c>{x:Bind local:PlayTimeButton.TimeSpanToString(PlayTimeTotal)}</c>→<see cref="PlayTimeText"/> 属性
/// （Avalonia 不能绑定静态方法调用）；<c>[INotifyPropertyChanged]</c>→手写 <c>SetProperty</c>；
/// <c>new PlayTimeStatsDialog{...}.ShowAsync()</c>→<see cref="DialogService.ShowAsync"/>。
/// </remarks>
public sealed partial class PlayTimeButton : UserControl, INotifyPropertyChanged
{

    /// <summary>当前游戏（用于查询 <c>playtime_total_{gameBiz}</c> 并传给统计对话框）。</summary>
    public static readonly StyledProperty<GameBiz> CurrentGameBizProperty =
        AvaloniaProperty.Register<PlayTimeButton, GameBiz>(nameof(CurrentGameBiz));

    public GameBiz CurrentGameBiz
    {
        get => GetValue(CurrentGameBizProperty);
        set => SetValue(CurrentGameBizProperty, value);
    }


    private readonly ILogger<PlayTimeButton> _logger = AppConfig.GetLogger<PlayTimeButton>();



    public PlayTimeButton()
    {
        InitializeComponent();
        DataContext = this;
    }



    /// <summary>累计游戏时长。</summary>
    public TimeSpan PlayTimeTotal
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayTimeText));
        }
    }


    /// <summary>按钮上显示的累计时长文本（Avalonia 侧替代 XAML 静态方法绑定）。</summary>
    public string PlayTimeText => TimeSpanToString(PlayTimeTotal);



    private void UserControl_Loaded(object? sender, RoutedEventArgs e)
    {
        InitializePlayTime();
    }



    private void InitializePlayTime()
    {
        try
        {
            GameBiz gameBiz = CurrentGameBiz.IsBilibili() ? $"{CurrentGameBiz.Game}_cn" : CurrentGameBiz;
            PlayTimeTotal = DatabaseService.GetValue<TimeSpan>($"playtime_total_{gameBiz}", out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize play time");
        }
    }



    /// <summary>把时长格式化为「Xh Ym」（与上游逐字一致）。</summary>
    public static string TimeSpanToString(TimeSpan timeSpan)
    {
        int totalMinutes = (int)Math.Round(timeSpan.TotalMinutes);
        int hours = totalMinutes / 60, minutes = totalMinutes % 60;
        return $"{hours}h {minutes}m";
    }



    private async void Button_OpenStats_Click(object? sender, RoutedEventArgs e)
    {
        await OpenStatsDialogAsync();
    }



    private async Task OpenStatsDialogAsync()
    {
        await DialogService.ShowAsync(new PlayTimeStatsDialog
        {
            CurrentGameBiz = CurrentGameBiz,
        });
        InitializePlayTime();
    }



    #region INotifyPropertyChanged

    private PropertyChangedEventHandler? _propertyChanged;

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }


    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

}
