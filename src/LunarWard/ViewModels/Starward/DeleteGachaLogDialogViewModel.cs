using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Views.Starward;
using LunarWard.Launch;
using LunarWard.Launch.Database;
using LunarWard.Platform;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 「删除指定时间段的抽卡记录」对话框的数据侧。
/// </summary>
/// <remarks>
/// 1:1 复刻 Starward <c>Features/Gacha/DeleteGachaLogDialog.xaml</c>（96 行）：
/// 标题、UID 选择、记录数、开始/结束时间（日期 + 24 小时制时间）、
/// 6 个月提示、删除（带「确认删除」Flyout）与取消（强调色）。
/// <para>
/// 删除动作与 Starward 一致：<c>DELETE FROM 表 WHERE Uid=@uid AND Time BETWEEN</c>
/// （<see cref="GachaLogStore.DeleteRange"/>），更新记录后被删的区间不会再出现。
/// </para>
/// </remarks>
public sealed partial class DeleteGachaLogDialogViewModel : StarwardPageViewModel
{
    private readonly IReadOnlyList<GachaRecord> _records;

    private readonly string _gameKind;


    public DeleteGachaLogDialogViewModel(
        PageContext context,
        IReadOnlyList<GachaRecord> records,
        long? defaultUid,
        string gameKind) : base(context)
    {
        _records = records;
        _gameKind = gameKind;

        UidList = new ObservableCollection<long>(
            records.Select(r => r.Uid).Where(uid => uid > 0).Distinct().OrderBy(uid => uid));

        // 时间默认值照 Starward：开始 = 首条记录向前取整小时，结束 = 最后一条记录
        var ordered = records.OrderBy(r => r.Time).ToList();
        BeginDate = ordered.Count > 0 ? new DateTimeOffset(ordered[0].Time.Date) : new DateTimeOffset(DateTime.Today);
        BeginTime = ordered.Count > 0 ? new TimeSpan(ordered[0].Time.Hour, 0, 0) : TimeSpan.Zero;
        EndDate = ordered.Count > 0 ? new DateTimeOffset(ordered[^1].Time.Date) : new DateTimeOffset(DateTime.Today);
        EndTime = ordered.Count > 0
            ? new TimeSpan(ordered[^1].Time.Hour, 59, 59)
            : new TimeSpan(23, 59, 59);

        SelectUid = defaultUid is long uid && UidList.Contains(uid)
            ? uid
            : UidList.Count > 0 ? UidList[0] : null;

        RefreshSelection();
    }


    /// <summary>关闭对话框（取消或删除完成后由视图订阅）。</summary>
    public event Action? RequestClose;


    public ObservableCollection<long> UidList { get; }


    [ObservableProperty]
    private long? _selectUid;

    partial void OnSelectUidChanged(long? value) => RefreshSelection();


    [ObservableProperty]
    private DateTimeOffset? _beginDate;

    [ObservableProperty]
    private TimeSpan? _beginTime;

    [ObservableProperty]
    private DateTimeOffset? _endDate;

    [ObservableProperty]
    private TimeSpan? _endTime;

    partial void OnBeginDateChanged(DateTimeOffset? value) => RefreshSelection();

    partial void OnBeginTimeChanged(TimeSpan? value) => RefreshSelection();

    partial void OnEndDateChanged(DateTimeOffset? value) => RefreshSelection();

    partial void OnEndTimeChanged(TimeSpan? value) => RefreshSelection();


    [ObservableProperty]
    private string _selectedCountText = "";

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _showSixMonthsWarning;

    [ObservableProperty]
    private string? _errorText;


    /// <summary>本次会删掉多少条（Starward <c>TextBlock_SelectedCount</c>）。</summary>
    public int SelectedCount { get; private set; }


    /// <summary>删除按钮是否可用（Starward <c>Button_Delete.IsEnabled</c>，默认 false）。</summary>
    [ObservableProperty]
    private bool _canDelete;


    private DateTime Begin => BeginDate?.Date ?? DateTime.Today;

    private DateTime BeginMoment => Begin + (BeginTime ?? TimeSpan.Zero);

    private DateTime EndMoment => (EndDate?.Date ?? DateTime.Today) + (EndTime ?? TimeSpan.Zero);


    private void RefreshSelection()
    {
        ErrorText = null;
        if (SelectUid is not long uid || uid <= 0)
        {
            SelectedCount = 0;
            HasSelection = false;
            CanDelete = false;
            SelectedCountText = "";
            return;
        }

        var begin = BeginMoment;
        var end = EndMoment;
        if (end < begin)
        {
            ErrorText = Strings["Gacha.EndBeforeBegin"];
            SelectedCount = 0;
            HasSelection = false;
            CanDelete = false;
            SelectedCountText = "";
            return;
        }

        SelectedCount = _records.Count(r => r.Uid == uid && r.Time >= begin && r.Time <= end);
        HasSelection = true;
        CanDelete = SelectedCount > 0;
        SelectedCountText = string.Format(Strings["Gacha.SelectedCount"], SelectedCount);

        // Starward：时间段里有 6 个月以前的记录时提示（游戏接口只保留 6 个月）
        ShowSixMonthsWarning = _records.Any(r => r.Uid == uid
                                                 && r.Time >= begin
                                                 && r.Time <= end
                                                 && r.Time < DateTime.Now.AddMonths(-6));
    }


    /// <summary>真正的删除动作（Starward <c>DeleteCommand</c>）。</summary>
    [RelayCommand]
    private void Delete()
    {
        if (SelectUid is not long uid || uid <= 0 || !CanDelete)
        {
            return;
        }

        var begin = BeginMoment;
        var end = EndMoment;

        try
        {
            // 上游 DeleteGachaLogByTime：直接删数据库行
            GachaLogStore.OpenDefault(Context.Services.Paths.DataHome)
                          .DeleteRange(_gameKind, uid, begin, end);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            return;
        }

        DeleteConfirmed?.Invoke(uid, begin, end);
        RequestClose?.Invoke();
    }


    /// <summary>删除成功后由页面订阅，用来立即从内存里摘掉这些记录。</summary>
    public event Action<long, DateTime, DateTime>? DeleteConfirmed;


    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}
