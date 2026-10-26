using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.Localization;
using LunarWard.Services;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 统计卡 5★ / 4★ 列表里的一行（Starward <c>Features/Gacha/GachaLogItemEx.cs</c> 的展示子集）。
/// </summary>
/// <remarks>
/// <para>
/// 字段对应 <c>GachaLogItemEx</c> 的 <c>Name / Pity / Icon / IsUp / HasUpItem /
/// UpTextOpacity / IsPointerIn</c>。
/// </para>
/// <para>
/// <b>Icon</b>：Starward 用 <c>sc:CachedImage</c> 显示 <c>GenshinGachaInfo.Icon</c> 里的远程图标；
/// LunarWard 由 <see cref="GachaIconCache"/> 下载并磁盘缓存，没有图标 URL（老数据没同步过 wiki）
/// 时只显示 28×28 底色占位。
/// </para>
/// <para>
/// <b>IsUp</b>：Starward 的 <c>up!</c> 标记来自 <c>Features/Gacha/GachaNoUp.cs</c> 的
/// 卡池时间表 + <c>ItemId</c>；LunarWard 已把该表整份复制到
/// <c>LunarWard.Core/Gacha/GachaNoUp.cs</c>，读库后由 <c>GachaService.ApplyUpFlags</c>
/// 按同一算法重算 5★ 行的 UP 标记（常驻池没有该表，标记保持 false，与上游一致）。
/// </para>
/// </remarks>
public sealed partial class GachaStatsItemViewModel : ObservableObject
{
    public GachaStatsItemViewModel(
        string name,
        int pity,
        DateTime time,
        int gachaType,
        bool isPityRow = false,
        bool isUp = false,
        string? iconUrl = null)
    {
        Name = name;
        Pity = pity;
        Time = time;
        GachaType = gachaType;
        IsPityRow = isPityRow;
        IsUp = isUp;
        IconUrl = iconUrl;

        if (!string.IsNullOrWhiteSpace(iconUrl))
        {
            Icon = GachaIconCache.TryGet(iconUrl);
            if (Icon is null)
            {
                _ = LoadIconAsync(iconUrl);
            }
        }
    }


    public string Name { get; }

    /// <summary>物品图标 URL（Starward <c>GachaLogItemEx.Icon</c>）；null = 未同步 wiki。</summary>
    public string? IconUrl { get; }

    /// <summary>图标位图（28×28 显示）；异步加载完成后由 <see cref="GachaIconCache"/> 回填。</summary>
    [ObservableProperty]
    private Bitmap? _icon;

    /// <summary>本行是「已垫」占位行（Starward GachaLogService.cs:233-248 在列表首位插入）。</summary>
    public bool IsPityRow { get; }

    public int Pity { get; }

    /// <summary>所属 gacha_type（Starward <c>GachaLogItemEx.GachaType</c>，用于保底进度条配色）。</summary>
    public int GachaType { get; }

    public string PityText => Pity.ToString();

    public DateTime Time { get; }

    /// <summary>Starward <c>ToolTipService.ToolTip="{x:Bind Time.ToString('yyyy-MM-dd HH:mm:ss')}"</c>。</summary>
    public string TimeText => Time.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>这一发是 UP（接口的 <c>is_up</c>；原神老接口没有该字段）。</summary>
    public bool IsUp { get; }

    /// <summary>Starward <c>GachaLogItemEx.HasUpItem</c>：行尾的「up!」标记。</summary>
    public bool HasUpItem => IsUp;

    /// <summary>Starward <c>GachaLogItemEx.UpTextOpacity</c>：UP 时 1，否则 0。</summary>
    public double UpTextOpacity => IsUp ? 1 : 0;

    /// <summary>鼠标悬停在本行；Starward 会让同名的所有行一起高亮。</summary>
    [ObservableProperty]
    private bool _isPointerIn;


    private async Task LoadIconAsync(string url)
    {
        Bitmap? bitmap = await GachaIconCache.LoadAsync(url).ConfigureAwait(false);
        if (bitmap is not null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Icon = bitmap);
        }
    }
}


/// <summary>
/// 右侧 Pane 里的一张材料统计卡（Starward <c>GachaLogPage.xaml</c> 的
/// <c>GridView_GachaItemStats_*</c> 项模板：80 宽、图标 + 次数）。
/// </summary>
/// <remarks>
/// Starward 按 <c>ItemId</c> 分组；<c>GachaRecord</c> 没有 ItemId，这里按 <c>Name</c>
/// 分组（同名物品合并），数量与排序语义一致（稀有度降序 → 次数降序 → 时间降序）。
/// </remarks>
public sealed partial class GachaItemStatsViewModel : ObservableObject
{
    public GachaItemStatsViewModel(string name, int rankType, int count, int itemId = 0, string? iconUrl = null)
    {
        Name = name;
        RankType = rankType;
        Count = count;
        ItemId = itemId;
        IconUrl = iconUrl;

        if (!string.IsNullOrWhiteSpace(iconUrl))
        {
            Icon = GachaIconCache.TryGet(iconUrl);
            if (Icon is null)
            {
                _ = LoadIconAsync(iconUrl);
            }
        }
    }


    public string Name { get; }

    /// <summary>物品 Id（上游按它分组；0 = 老记录还没同步 wiki）。</summary>
    public int ItemId { get; }

    /// <summary>图标 URL（Starward <c>GachaLogItemEx.Icon</c>）。</summary>
    public string? IconUrl { get; }

    /// <summary>图标位图；null 时卡片显示名称占位（上述上游无素材时的降级）。</summary>
    [ObservableProperty]
    private Bitmap? _icon;

    public bool HasIcon => Icon is not null;

    /// <summary>3 / 4 / 5 星，决定占位底色。</summary>
    public int RankType { get; }

    public int Count { get; }

    /// <summary>"12 次"（Starward GachaLogPage.xaml:377-384 的 ItemCount + SpiralAbyssPage_Times）。</summary>
    public string CountText => $"{Count} {AppStrings.Instance["Gacha.Times"]}";


    partial void OnIconChanged(Bitmap? value) => OnPropertyChanged(nameof(HasIcon));


    private async Task LoadIconAsync(string url)
    {
        Bitmap? bitmap = await GachaIconCache.LoadAsync(url).ConfigureAwait(false);
        if (bitmap is not null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Icon = bitmap);
        }
    }
}


/// <summary>
/// 「隐藏卡池」Flyout 里的一项（Starward <c>GachaBanner</c> + <c>ListView_GachaBanners</c>）。
/// </summary>
/// <remarks>
/// Starward 用 <c>ListView SelectionMode="Multiple"</c>，选中项 = 要显示的卡池，
/// 没有任何选中项时显示全部（GachaLogPage.xaml.cs:381-384）。这里用 CheckBox 复刻同一语义，
/// 勾选状态通过 <c>Value</c> 回调给页面 VM。
/// </remarks>
public sealed partial class GachaBannerViewModel : ObservableObject
{
    private readonly Action<GachaBannerViewModel> _selectionChanged;


    public GachaBannerViewModel(int value, string text, Action<GachaBannerViewModel> selectionChanged)
    {
        Value = value;
        Text = text;
        _selectionChanged = selectionChanged;
    }


    public int Value { get; }

    public string Text { get; }

    /// <summary>勾选 = 显示该卡池；全部不勾选时显示全部。</summary>
    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => _selectionChanged(this);
}
