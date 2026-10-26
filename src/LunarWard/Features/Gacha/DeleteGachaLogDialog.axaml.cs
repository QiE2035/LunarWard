using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using LunarWard.Core;
using LunarWard.Helpers;
using LunarWard.Frameworks.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;


namespace LunarWard.Features.Gacha;

/// <summary>
/// 上游 <c>Starward.Features.Gacha.DeleteGachaLogDialog</c> 的移植（按时间段删除抽卡记录）。
/// </summary>
/// <remarks>
/// 平台差异（docs/09）：上游 <c>ContentDialog</c> → <see cref="StarwardContentDialog"/>；
/// <c>Visibility</c> → <see cref="Avalonia.Controls.IsVisibleProperty"/>；
/// <c>this.Hide()</c> → <see cref="StarwardContentDialog.Close"/>；
/// <c>[INotifyPropertyChanged]</c> → 手写 <c>SetProperty</c>（Avalonia 控件已实现 INPC）。
/// </remarks>
public sealed partial class DeleteGachaLogDialog : StarwardContentDialog, INotifyPropertyChanged
{


    private readonly ILogger<DeleteGachaLogDialog> _logger = AppConfig.GetLogger<DeleteGachaLogDialog>();


    public GameBiz CurrentGameBiz { get; set; }


    public long? DefaultUid { get; set; }


    public bool Deleted { get; set; }



    private GachaLogService _gachaLogService;


    public DeleteGachaLogDialog()
    {
        this.InitializeComponent();
        DataContext = this;
    }




    private ObservableCollection<long>? _uidList;

    public ObservableCollection<long>? UidList
    {
        get => _uidList;
        set => SetProperty(ref _uidList, value);
    }



    private long? _selectUid;

    public long? SelectUid
    {
        get => _selectUid;
        set
        {
            if (SetProperty(ref _selectUid, value))
            {
                if (value.HasValue)
                {
                    LoadGachaLog(value.Value);
                }
            }
        }
    }


    private List<GachaLogItemEx> gachalogs;


    private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (CurrentGameBiz.Game is GameBiz.hk4e)
            {
                _gachaLogService = AppConfig.GetService<GenshinGachaService>();
            }
            if (CurrentGameBiz.Game is GameBiz.hkrpg)
            {
                _gachaLogService = AppConfig.GetService<StarRailGachaService>();
            }
            if (CurrentGameBiz.Game is GameBiz.nap)
            {
                _gachaLogService = AppConfig.GetService<ZZZGachaService>();
            }

            if (_gachaLogService is not null)
            {
                UidList = new(_gachaLogService.GetUids());
                if (DefaultUid.HasValue && UidList.Contains(DefaultUid.Value))
                {
                    SelectUid = DefaultUid;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete gacha log dialog initialize.");
        }
    }



    private void LoadGachaLog(long uid)
    {
        try
        {
            gachalogs = _gachaLogService.GetGachaLogItemEx(uid);
            TextBlock_GachaLogNumber.IsVisible = true;
            TextBlock_GachaLogNumber.Text = string.Format(Lang.DeleteGachaLogDialog_ThisAccountHas0GachaRecordS, gachalogs.Count);
            CalendarDatePicker_BeginTime.SelectedDate = null;
            CalendarDatePicker_EndTime.SelectedDate = null;
            TimePicker_BeginTime.SelectedTime = null;
            TimePicker_EndTime.SelectedTime = null;
            Button_Delete.IsEnabled = false;
            TextBlock_SelectedCount.IsVisible = false;
            TextBlock_Warning.IsVisible = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete gacha log dialog load gacha log.");
        }
    }




    private void CalendarDatePicker_DateChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (sender == CalendarDatePicker_BeginTime && TimePicker_BeginTime.SelectedTime == null)
        {
            TimePicker_BeginTime.SelectedTime = TimeSpan.Zero;
        }
        if (sender == CalendarDatePicker_EndTime && TimePicker_EndTime.SelectedTime == null)
        {
            TimePicker_EndTime.SelectedTime = TimeSpan.Zero;
        }
        OnTimePeriodChanged();
    }



    private void TimePicker_SelectedTimeChanged(object? sender, TimePickerSelectedValueChangedEventArgs args)
    {
        OnTimePeriodChanged();
    }




    private void OnTimePeriodChanged()
    {
        try
        {
            var beginDate = CalendarDatePicker_BeginTime.SelectedDate;
            var beginTime = TimePicker_BeginTime.SelectedTime;
            var endDate = CalendarDatePicker_EndTime.SelectedDate;
            var endTime = TimePicker_EndTime.SelectedTime;
            if (beginDate.HasValue && beginTime.HasValue && endDate.HasValue && endTime.HasValue)
            {
                var begin = beginDate + beginTime;
                var end = endDate + endTime;
                if (begin <= end)
                {
                    var count = gachalogs.Count(x => x.Time >= begin && x.Time <= end);
                    TextBlock_SelectedCount.IsVisible = true;
                    TextBlock_SelectedCount.Text = string.Format(Lang.DeleteGachaLogDialog_TheSelectedTimePeriodIncludes0GachaRecords, count);
                    Button_Delete.IsEnabled = count > 0;
                    TextBlock_Warning.IsVisible = begin < DateTimeOffset.Now - TimeSpan.FromDays(180);
                    return;
                }
            }
            Button_Delete.IsEnabled = false;
            TextBlock_SelectedCount.IsVisible = false;
            TextBlock_Warning.IsVisible = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete gacha log dialog time changed.");
        }
    }





    [RelayCommand]
    private void Delete()
    {
        try
        {
            var beginDate = CalendarDatePicker_BeginTime.SelectedDate;
            var beginTime = TimePicker_BeginTime.SelectedTime;
            var endDate = CalendarDatePicker_EndTime.SelectedDate;
            var endTime = TimePicker_EndTime.SelectedTime;
            if (SelectUid.HasValue && beginDate.HasValue && beginTime.HasValue && endDate.HasValue && endTime.HasValue)
            {
                var begin = beginDate + beginTime;
                var end = endDate + endTime;
                if (begin <= end)
                {
                    var count = gachalogs.Count(x => x.Time >= begin && x.Time <= end);
                    if (count > 0)
                    {
                        _logger.LogInformation("Deleting {count} gachalogs from {begin} to {end} of {uid} ({biz}).", count, begin, end, SelectUid, CurrentGameBiz);
                        _gachaLogService.DeleteGachaLogByTime(SelectUid.Value, begin.Value, end.Value);
                        InAppToast.MainWindow?.Success(string.Format(Lang.GachaLogPage_DeletedGachaRecordsOfUid, count, SelectUid));
                        Deleted = true;
                        this.Close();
                        return;
                    }
                }
            }
            Button_Delete.IsEnabled = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete gacha log dialog delete method.");
        }
    }



    [RelayCommand]
    private void Cancel()
    {
        this.Close();
    }




    #region INotifyPropertyChanged

    /// <summary>
    /// Avalonia 的 <see cref="AvaloniaObject"/> 已实现 <see cref="INotifyPropertyChanged"/>，但只对其
    /// AvaloniaProperty 发通知；本类的 CLR 属性走这里。用 <c>new</c> 隐藏基类事件，并把接口分发显式重映射过来。
    /// </summary>
    public new event PropertyChangedEventHandler? PropertyChanged;

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => PropertyChanged += value;
        remove => PropertyChanged -= value;
    }


    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    #endregion


}
