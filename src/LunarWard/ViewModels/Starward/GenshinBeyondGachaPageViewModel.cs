using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Input.Platform;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Services;
using LunarWard.Launch.Database;
using LunarWard.Launch.Gacha;
using LunarWard.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.Gacha.Genshin;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 千星奇域颂愿（Starward <c>GenshinBeyondGachaPage</c>，<c>Features/Gacha/GenshinBeyondGachaPage.xaml.cs</c>）。
///
/// <para>页面只有两张统计卡（常驻颂愿 1000 / 活动颂愿 2000）+ 右侧「材料统计」Pane，
/// 数据来自 <c>hk4eugc</c> 独立的抽卡接口（<see cref="GenshinBeyondGachaClient"/>）。
/// 上游把记录写进 SQLite；这里由 <see cref="GenshinBeyondGachaService"/> 写同一个库
/// 统计口径（保底、平均、比例、倒序列表 + 头部「已垫」占位行）逐行对齐源码。</para>
/// </summary>
public sealed partial class GenshinBeyondGachaPageViewModel : StarwardPageViewModel
{

    /// <summary>千星奇域颂愿的 GameBiz（与本体 <c>hk4e_cn</c> 不同）。</summary>
    private const string BeyondGameBiz = "hk4eugc";

    private readonly GenshinBeyondGachaService _service;

    private readonly string _gameDirectory;

    private bool _disposed;


    public GenshinBeyondGachaPageViewModel(PageContext context) : base(context)
    {
        _gameDirectory = LunarWard.Launch.ProfileConfig.ExpandPath(Profile.Profile.Path);

        string file = CaptureMode.ResolveDatabaseFile(Services.Paths.DataHome);
        _service = new GenshinBeyondGachaService(databaseFile: file);

        UidList = [];
        ItemStats = [];
        GachaLanguage = "zh-cn";

        Reload();
    }


    // ── 页头 ──────────────────────────────────────────────────────────

    /// <summary>Starward <c>Lang.GenshinBeyondGachaPage_MiliastraWonderlandOde</c>。</summary>
    public string PageTitle => Strings["Gacha.Title.Beyond"];

    public ObservableCollection<long> UidList { get; }

    [ObservableProperty]
    private long? _selectUid;

    partial void OnSelectUidChanged(long? value)
    {
        UpdateGachaTypeStats(value);
    }


    // ── 状态 ──────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _statusText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    private bool _isBusy;

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    public bool ShowStatusArea => HasStatus || HasError;

    /// <summary>Starward <c>GachaLanguage</c>：仅用于请求接口时的语言参数。</summary>
    public string GachaLanguage { get; private set; }


    // ── 两张统计卡 ────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStats), nameof(ShowEmptyState))]
    private GenshinBeyondGachaCardViewModel? _card1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStats), nameof(ShowEmptyState))]
    private GenshinBeyondGachaCardViewModel? _card2000;

    public bool HasStats => Card1000 is not null || Card2000 is not null;

    /// <summary>Starward <c>StackPanel_Emoji.Visibility</c>：两张卡都没有数据时显示空态。</summary>
    public bool ShowEmptyState => !HasStats;


    // ── 材料统计 Pane ─────────────────────────────────────────────────

    public ObservableCollection<GenshinBeyondGachaItemCardViewModel> ItemStats { get; }

    /// <summary>Starward <c>SplitView_Content.IsPaneOpen</c>。</summary>
    [ObservableProperty]
    private bool _isPaneOpen;

    /// <summary>Pane 按钮的可用性看有没有材料统计（Starward 绑 <c>GachaItemStats</c> 非空）。</summary>
    public bool HasItemStats => ItemStats.Count > 0;


    // ── 命令 ──────────────────────────────────────────────────────────

    /// <summary>Starward <c>UpdateGachaLogCommand</c>；<paramref name="param"/> = null / "cache" / "all"。</summary>
    [RelayCommand]
    private async Task UpdateGachaLogAsync(string? param)
    {
        try
        {
            string? url = null;
            if (param is "cache")
            {
                if (SelectUid is null or 0)
                {
                    return;
                }
                url = _service.GetGachaLogUrlByUid(SelectUid.Value);
                if (string.IsNullOrWhiteSpace(url))
                {
                    ErrorText = string.Format(Strings["Gacha.SavedUrlMissing"], SelectUid);
                    return;
                }
            }
            else
            {
                if (!Directory.Exists(_gameDirectory))
                {
                    ErrorText = Strings["Gacha.NoGameDirectory"];
                    return;
                }
                url = _service.GetGachaLogUrlFromWebCache(new GameBiz(BeyondGameBiz), _gameDirectory);
                if (string.IsNullOrWhiteSpace(url))
                {
                    ErrorText = Strings["Gacha.UrlMissingHint"];
                    return;
                }
            }
            await UpdateGachaLogInternalAsync(url, param is "all");
        }
        catch (Exception ex)
        {
            Log.Warn($"千星奇域颂愿记录读取失败：{ex.Message}");
            ErrorText = ex.Message;
        }
    }


    private async Task UpdateGachaLogInternalAsync(string url, bool all)
    {
        IsBusy = true;
        ErrorText = null;
        StatusText = Strings["Gacha.FetchingUid"];
        var progress = new Progress<(int GachaType, int Page)>(p => StatusText = string.Format(
            Strings["Gacha.ProgressText"],
            p.GachaType == 1000 ? Strings["Gacha.OdeStandard"] : Strings["Gacha.OdeEvent"],
            p.Page));
        try
        {
            var uid = await _service.GetGachaLogAsync(url, all, GachaLanguage, progress);
            if (_disposed)
            {
                return;
            }
            Reload();
            if (uid == 0)
            {
                ErrorText = Strings["Gacha.NoRecordIn6Months"];
                return;
            }
            SelectUid = uid;
            if (!UidList.Contains(uid))
            {
                UidList.Add(uid);
            }
            StatusText = string.Format(Strings["Gacha.FetchDone"], _service.GetGachaItemStats(uid)?.Sum(x => x.Count) ?? 0);
        }
        finally
        {
            IsBusy = false;
        }
    }


    /// <summary>Starward <c>InputUrlCommand</c>：对话框由视图负责，确认后回到 <see cref="ApplyInputUrlAsync"/>。</summary>
    [RelayCommand]
    private void OpenItemStatsPane()
    {
        IsPaneOpen = !IsPaneOpen;
    }



    /// <summary>视图在「输入 URL」对话框确认后调用（Starward 在同一方法里等对话框返回）。</summary>
    public async Task ApplyInputUrlAsync(string url)
    {
        try
        {
            ErrorText = null;
            await UpdateGachaLogInternalAsync(url, false);
        }
        catch (Exception ex)
        {
            Log.Warn($"千星奇域颂愿 URL 读取失败：{ex.Message}");
            ErrorText = ex.Message;
        }
    }


    /// <summary>Starward <c>CopyUrlCommand</c>：把当前 UID 最后一次成功的 URL 写进剪贴板。</summary>
    public async Task<string?> GetCopyableUrlAsync()
    {
        if (SelectUid is null or 0)
        {
            return null;
        }
        var url = _service.GetGachaLogUrlByUid(SelectUid.Value);
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }
        var clipboard = (Avalonia.Application.Current?.ApplicationLifetime as
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;
        if (clipboard is null)
        {
            return null;
        }
        await clipboard.SetTextAsync(url);
        return url;
    }


    /// <summary>Starward <c>DeleteGachaCacheFolderCommand</c> 在当前实现里是打开缓存目录（如 GachaPage）。</summary>
    [RelayCommand]
    private void OpenCacheFolder()
    {
        var folder = GenshinBeyondGachaClient.GetWebCachesFolderPath(new GameBiz(BeyondGameBiz), _gameDirectory);
        try
        {
            Directory.CreateDirectory(folder);
            var info = new System.Diagnostics.ProcessStartInfo { FileName = "xdg-open", UseShellExecute = false };
            info.ArgumentList.Add(folder);
            using var process = System.Diagnostics.Process.Start(info);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    // ── 数据 ──────────────────────────────────────────────────────────

    private void Reload()
    {
        UidList.Clear();
        foreach (var uid in _service.GetUids())
        {
            UidList.Add(uid);
        }
        if (SelectUid is null || !UidList.Contains(SelectUid.Value))
        {
            SelectUid = UidList.FirstOrDefault();
            UpdateGachaTypeStats(SelectUid);
        }
        else
        {
            UpdateGachaTypeStats(SelectUid);
        }
    }


    /// <summary>Starward <c>UpdateGachaTypeStats</c>。</summary>
    private void UpdateGachaTypeStats(long? uid)
    {
        Card1000 = null;
        Card2000 = null;
        ItemStats.Clear();

        if (uid is > 0)
        {
            var stats1000 = _service.GetGachaTypeStatsType1000(uid.Value);
            var stats2000 = _service.GetGachaTypeStatsType2000(uid.Value);
            Card1000 = stats1000 is null ? null : new GenshinBeyondGachaCardViewModel(stats1000, Strings, 1000);
            Card2000 = stats2000 is null ? null : new GenshinBeyondGachaCardViewModel(stats2000, Strings, 2000);

            foreach (var item in _service.GetGachaItemStats(uid.Value) ?? [])
            {
                ItemStats.Add(new GenshinBeyondGachaItemCardViewModel(item));
            }
        }

        OnPropertyChanged(nameof(HasStats));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(HasItemStats));
    }


    public override void Dispose()
    {
        _disposed = true;
        base.Dispose();
    }

}



/// <summary>
/// 一张统计卡（Starward <c>GenshinBeyondGachaTypeStats</c> 的展示形态）。
///
/// <para>源码里这两张卡的差别只有「按 4★ 还是 5★ 计保底/平均」，
/// 这里把四个统计行统一成 <see cref="Rows"/>，模板只写一遍。</para>
/// </summary>
public sealed partial class GenshinBeyondGachaCardViewModel : ObservableObject
{

    private static readonly IBrush Rarity5 = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00));

    private static readonly IBrush Rarity4 = new SolidColorBrush(Color.FromRgb(0xC8, 0x64, 0xE0));

    private static readonly IBrush Rarity3 = new SolidColorBrush(Color.FromRgb(0x5E, 0xAA, 0xED));

    private static readonly IBrush Rarity2 = new SolidColorBrush(Color.FromRgb(0x3B, 0xA2, 0x72));


    public GenshinBeyondGachaCardViewModel(GenshinBeyondGachaTypeStats stats, Localization.AppStrings strings, int gachaType)
    {
        Title = gachaType == 1000 ? strings["Gacha.OdeStandard"] : strings["Gacha.OdeEvent"];
        CountText = stats.Count.ToString(CultureInfo.CurrentCulture);
        RangeText = $"{stats.StartTime.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.CurrentCulture)} - " +
                    $"{stats.EndTime.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.CurrentCulture)}";

        // 源码：1000 卡是「4★ 平均 / 4★ 统计 / 3★ 统计 / 2★ 统计」；
        // 2000 卡是「5★ 平均 / 5★ 统计 / 4★ 统计 / 3★ 统计」。
        var averageLabel = strings["Gacha.Average"];
        var statsLabel = strings["Gacha.Stats"];
        Rows = gachaType == 1000
            ? [
                new("4★" + averageLabel, stats.Average_4.ToString("F2", CultureInfo.CurrentCulture), Rarity4),
                new("4★" + statsLabel, FormatCount(stats.Count_4, stats.Ratio_4), Rarity4),
                new("3★" + statsLabel, FormatCount(stats.Count_3, stats.Ratio_3), Rarity3),
                new("2★" + statsLabel, FormatCount(stats.Count_2, stats.Ratio_2), Rarity2),
            ]
            : [
                new("5★" + averageLabel, stats.Average_5.ToString("F2", CultureInfo.CurrentCulture), Rarity5),
                new("5★" + statsLabel, FormatCount(stats.Count_5, stats.Ratio_5), Rarity5),
                new("4★" + statsLabel, FormatCount(stats.Count_4, stats.Ratio_4), Rarity4),
                new("3★" + statsLabel, FormatCount(stats.Count_3, stats.Ratio_3), Rarity3),
            ];

        // 源码 Pivot 的两个页签：1000 = 4★/3★，2000 = 5★/4★；每页头部都有「已垫」占位行。
        List1 = BuildRows(gachaType == 1000 ? stats.List_4 : stats.List_5, strings);
        List2 = BuildRows(gachaType == 1000 ? stats.List_3 : stats.List_4, strings);
        Tab1Text = gachaType == 1000 ? "4★" : "5★";
        Tab2Text = gachaType == 1000 ? "3★" : "4★";
        Tab1Brush = gachaType == 1000 ? Rarity4 : Rarity5;
        Tab2Brush = gachaType == 1000 ? Rarity3 : Rarity4;
        SelectedTabIndex = 0;
    }


    private static string FormatCount(int count, double ratio) => $"{count} [{ratio.ToString("P2", CultureInfo.CurrentCulture)}]";


    private static List<GenshinBeyondGachaPityRowViewModel> BuildRows(List<GenshinBeyondGachaItemEx> items, Localization.AppStrings strings)
    {
        // 头部占位行由 Core 插入（ItemId 为 0），文案在 App 层本地化。
        return items.Select(x => new GenshinBeyondGachaPityRowViewModel(
            x.Id == 0 ? strings["Gacha.PityPlaceholder"] : x.ItemName,
            x.Pity,
            x.Time)).ToList();
    }


    public string Title { get; }

    public string CountText { get; }

    public string RangeText { get; }

    public IReadOnlyList<GenshinBeyondGachaRowViewModel> Rows { get; }

    public List<GenshinBeyondGachaPityRowViewModel> List1 { get; }

    public List<GenshinBeyondGachaPityRowViewModel> List2 { get; }

    public string Tab1Text { get; }

    public string Tab2Text { get; }

    public IBrush Tab1Brush { get; }

    public IBrush Tab2Brush { get; }

    /// <summary>页签底：选中项浅色胶囊（Starward 的 Segmented 控件效果）。</summary>
    public IBrush Tab1Background => SelectedTabIndex == 0 ? TabSelectedBrush : TabUnselectedBrush;

    /// <summary>页签底：未选中项透明。</summary>
    public IBrush Tab2Background => SelectedTabIndex == 1 ? TabSelectedBrush : TabUnselectedBrush;


    private static readonly IBrush TabSelectedBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));

    private static readonly IBrush TabUnselectedBrush = Brushes.Transparent;

    /// <summary>0 = <see cref="List1"/>，1 = <see cref="List2"/>（对应 Starward 的 Pivot + Segmented）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleList), nameof(Tab1Background), nameof(Tab2Background))]
    private int _selectedTabIndex;

    public List<GenshinBeyondGachaPityRowViewModel> VisibleList => SelectedTabIndex == 0 ? List1 : List2;

}


/// <summary>统计卡里一行「4★平均 12.34」。</summary>
public sealed class GenshinBeyondGachaRowViewModel(string label, string value, IBrush brush)
{

    public string Label { get; } = label;

    public string Value { get; } = value;

    public IBrush Brush { get; } = brush;

}



/// <summary>
/// 保底列表的一行（Starward <c>GenshinBeyondGachaItemEx</c> 模板）：
/// 图标 + 名称 + 已垫抽数，名称后面是整行宽度按保底进度渐变的底色。
/// </summary>
/// <remarks>
/// 图标是上游 <c>CachedImage</c> 从 <c>GenshinBeyondGachaInfo</c> 取的远程 PNG；
/// LunarWard 目前没有远程图片加载控件，这里只显示名称（与 GachaPage 的取舍一致）。
/// </remarks>
public sealed class GenshinBeyondGachaPityRowViewModel
{

    public GenshinBeyondGachaPityRowViewModel(string name, int pity, DateTime time)
    {
        Name = name;
        PityText = pity.ToString(CultureInfo.CurrentCulture);
        TimeText = time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        Background = CreateBackground(pity);
    }


    /// <summary>
    /// Starward <c>GenshinBeyondGachaPityProgressBackgroundBrushConverter</c>：
    /// 阈值 64 抽、保底 70 抽，未到阈值绿色、到阈值红色，横向渐变、整体 Opacity 0.4。
    /// </summary>
    private static IBrush CreateBackground(int pity)
    {
        var color = pity < 64
            ? Color.FromRgb(0x00, 0xE0, 0x79)
            : Color.FromRgb(0xC8, 0x3C, 0x23);
        var offset = Math.Clamp(pity / 70.0, 0, 1);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
            EndPoint = new Avalonia.RelativePoint(1, 0, Avalonia.RelativeUnit.Relative),
            Opacity = 0.4,
        };
        brush.GradientStops.Add(new GradientStop(color, 0));
        brush.GradientStops.Add(new GradientStop(color, offset));
        brush.GradientStops.Add(new GradientStop(Colors.Transparent, offset));
        return brush;
    }


    public string Name { get; }

    public string PityText { get; }

    public string TimeText { get; }

    public IBrush Background { get; }

}



/// <summary>材料统计 Pane 的一张卡片（80×97，稀有度底色 + 名称 + 次数）。</summary>
public sealed class GenshinBeyondGachaItemCardViewModel
{

    public GenshinBeyondGachaItemCardViewModel(GenshinBeyondGachaItemEx item)
    {
        Name = item.ItemName;
        CountText = item.Count.ToString(CultureInfo.CurrentCulture);
        RankType = item.RankType;
        Background = item.RankType switch
        {
            5 => new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00), 0.28),
            4 => new SolidColorBrush(Color.FromRgb(0xC8, 0x64, 0xE0), 0.28),
            3 => new SolidColorBrush(Color.FromRgb(0x5E, 0xAA, 0xED), 0.28),
            _ => new SolidColorBrush(Color.FromRgb(0x3B, 0xA2, 0x72), 0.28),
        };
    }


    public string Name { get; }

    public string CountText { get; }

    public int RankType { get; }

    public IBrush Background { get; }

}
