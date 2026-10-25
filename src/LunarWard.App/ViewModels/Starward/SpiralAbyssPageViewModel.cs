using LunarWard.App.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.Genshin.SpiralAbyss;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 深境螺旋页 VM（Starward <c>Features/GameRecord/Genshin/SpiralAbyssPage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 左侧是「当期 / 上期」两个周期（Starward 从 SQLite 读缓存列表；LunarWard 每次进页
/// 直接调 <c>spiralAbyss?schedule_type=1/2</c>），右侧是当期统计、出战榜、战斗数据榜和
/// 每层每间的阵容。
/// </para>
/// <para>
/// 头像来自米游社 CDN（<c>avatar_icon</c> 字段），用 <see cref="Services.StarwardAssets.BitmapAsync"/>
/// 下载并缓存；没有网络/图床失败时只显示缺省底色，不影响文字数据。
/// </para>
/// </remarks>
public sealed partial class SpiralAbyssPageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    private bool _initialized;


    public SpiralAbyssPageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _ = InitializeAsync();
    }


    /// <summary>左侧周期列表（Starward <c>ListView_AbyssList</c>）。</summary>
    public ObservableCollection<AbyssPeriodItem> AbyssList { get; } = [];

    [ObservableProperty]
    private AbyssPeriodItem? _currentAbyss;

    partial void OnCurrentAbyssChanged(AbyssPeriodItem? value)
    {
        ApplyCurrentAbyss(value);
    }


    /// <summary>有战斗数据（Starward <c>HasData</c>）：隐藏"出战/战斗数据/层"三块。</summary>
    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>两个周期都没有数据（Starward 的派蒙表情空态）。</summary>
    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private string? _errorText;

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>XAML 的页内错误提示是否显示。</summary>
    public bool HasError => ErrorText is not null;


    // ── 右侧详情 ────────────────────────────────────────────────────

    [ObservableProperty]
    private string _periodText = "";

    [ObservableProperty]
    private string _totalStarText = "";

    [ObservableProperty]
    private string _maxFloorText = "";

    [ObservableProperty]
    private string _battleCountText = "";

    /// <summary>出战榜（Starward <c>RevealRank</c>）。</summary>
    public ObservableCollection<AbyssAvatarItem> RevealRanks { get; } = [];

    /// <summary>战斗数据榜（Starward 的 5 个 <c>FirstXxxRank</c>）。</summary>
    public ObservableCollection<AbyssRankRow> CombatRows { get; } = [];

    /// <summary>层 / 间 / 上下半阵容。</summary>
    public ObservableCollection<AbyssFloorItem> Floors { get; } = [];


    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_initialized)
        {
            await InitializeAsync();
        }
    }


    private async Task InitializeAsync()
    {
        _initialized = true;

        if (ToolboxDemo.Enabled)
        {
            ApplyDemoAbyss();
            return;
        }

        IsLoading = true;
        ErrorText = null;
        try
        {
            var infos = new List<SpiralAbyssInfo>();
            for (var schedule = 1; schedule <= 2; schedule++)
            {
                try
                {
                    infos.Add(await _client.GetSpiralAbyssInfoAsync(_role, schedule).ConfigureAwait(true));
                }
                catch (miHoYoApiException)
                {
                    // 该期没打过 / 没解锁：Starward 的缓存里也只会少这一条。
                }
            }

            var selected = CurrentAbyss?.Info.ScheduleId;
            AbyssList.Clear();
            foreach (var info in infos)
            {
                AbyssList.Add(new AbyssPeriodItem(info));
            }

            ShowEmpty = AbyssList.Count == 0;
            if (ShowEmpty)
            {
                CurrentAbyss = null;
                return;
            }

            CurrentAbyss = AbyssList.FirstOrDefault(x => x.Info.ScheduleId == selected) ?? AbyssList[0];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorText = string.Format(Strings["SpiralAbyss.NetworkError"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["SpiralAbyss.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }


    /// <summary>
    /// 自检/截图夹具（<c>LUNARWARD_TOOLBOX_DEMO=1</c>）：不联网，给页面灌两期演示数据。
    /// 与 Starward 的 x:DesignInstance 同用途；正常启动不会走到这里。
    /// </summary>
    private void ApplyDemoAbyss()
    {
        AbyssList.Clear();
        var now = DateTimeOffset.Now;
        for (var i = 0; i < 2; i++)
        {
            AbyssList.Add(new AbyssPeriodItem(BuildDemoInfo(now.AddDays(-14 * i), 36 - i * 3, 2 - i)));
        }

        ShowEmpty = false;
        ErrorText = null;
        CurrentAbyss = AbyssList[0];
    }


    private static SpiralAbyssInfo BuildDemoInfo(DateTimeOffset start, int totalStar, int schedule)
    {
        var floors = new List<SpiralAbyssFloor>();
        for (var f = 12; f >= 11; f--)
        {
            var levels = new List<SpiralAbyssLevel>();
            for (var l = 1; l <= 3; l++)
            {
                levels.Add(new SpiralAbyssLevel
                {
                    Index = l,
                    Star = 3,
                    MaxStar = 3,
                    Battles =
                    [
                        new SpiralAbyssBattle { Index = 1, Time = start.AddDays(1), Avatars = DemoAvatars(f + l) },
                        new SpiralAbyssBattle { Index = 2, Time = start.AddDays(2), Avatars = DemoAvatars(f + l + 4) },
                    ],
                });
            }

            floors.Add(new SpiralAbyssFloor { Index = f, Star = 9, MaxStar = 9, Levels = levels });
        }

        return new SpiralAbyssInfo
        {
            ScheduleId = schedule,
            StartTime = start,
            EndTime = start.AddDays(14),
            TotalStar = totalStar,
            MaxFloor = "12",
            TotalBattleCount = 12,
            RevealRank = DemoRank(4, 6),
            DefeatRank = [new SpiralAbyssRank { Rarity = 5, Value = 1234 }],
            DamageRank = [new SpiralAbyssRank { Rarity = 5, Value = 987654 }],
            TakeDamageRank = [new SpiralAbyssRank { Rarity = 5, Value = 123456 }],
            NormalSkillRank = [new SpiralAbyssRank { Rarity = 5, Value = 876 }],
            EnergySkillRank = [new SpiralAbyssRank { Rarity = 5, Value = 654 }],
            Floors = floors,
            IsUnlock = true,
        };
    }


    private static List<SpiralAbyssRank> DemoRank(int count, int value)
    {
        var list = new List<SpiralAbyssRank>();
        for (var i = 0; i < count; i++)
        {
            list.Add(new SpiralAbyssRank { Rarity = i % 2 == 0 ? 5 : 4, Value = value - i });
        }

        return list;
    }


    private static List<SpiralAbyssAvatar> DemoAvatars(int seed)
    {
        var list = new List<SpiralAbyssAvatar>();
        for (var i = 0; i < 4; i++)
        {
            list.Add(new SpiralAbyssAvatar { Rarity = i % 2 == 0 ? 5 : 4, Level = 90 });
        }

        return list;
    }


    private void ApplyCurrentAbyss(AbyssPeriodItem? item)
    {
        RevealRanks.Clear();
        CombatRows.Clear();
        Floors.Clear();
        TotalStarText = "";
        MaxFloorText = "";
        BattleCountText = "";
        PeriodText = "";

        if (item is null)
        {
            HasData = false;
            return;
        }

        var info = item.Info;
        PeriodText = string.Format(
            Strings["SpiralAbyss.PeriodRange"],
            info.StartTime.LocalDateTime.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture),
            info.EndTime.LocalDateTime.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture));
        TotalStarText = info.TotalStar.ToString(System.Globalization.CultureInfo.InvariantCulture);
        MaxFloorText = string.IsNullOrWhiteSpace(info.MaxFloor)
            ? "—"
            : string.Format(Strings["SpiralAbyss.FloorX"], info.MaxFloor);
        BattleCountText = info.TotalBattleCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        HasData = info.TotalBattleCount > 0;

        if (info.RevealRank is { Count: > 0 } reveal)
        {
            foreach (var rank in reveal)
            {
                RevealRanks.Add(new AbyssAvatarItem(rank.AvatarIcon, rank.Rarity, 0, rank.Value));
            }
        }

        CombatRows.Add(new AbyssRankRow(Strings["SpiralAbyss.MostDefeats"], info.FirstDefeatRank, 0));
        CombatRows.Add(new AbyssRankRow(Strings["SpiralAbyss.StrongestSingleStrike"], info.FirstDamageRank, 1));
        CombatRows.Add(new AbyssRankRow(Strings["SpiralAbyss.MostDamageTaken"], info.FirstTakeDamageRank, 2));
        CombatRows.Add(new AbyssRankRow(Strings["SpiralAbyss.ElementalSkillsUnleashed"], info.FirstNormalSkillRank, 3));
        CombatRows.Add(new AbyssRankRow(Strings["SpiralAbyss.ElementalBurstsUnleashed"], info.FirstEnergySkillRank, 4));

        if (info.Floors is { Count: > 0 } floors)
        {
            foreach (var floor in floors)
            {
                Floors.Add(new AbyssFloorItem(floor, Strings));
            }
        }

        _ = LoadIconsAsync(info);
    }


    /// <summary>把本期所有头像下载到内存（先在 UI 线程建好占位，再逐张填图标）。</summary>
    private async Task LoadIconsAsync(SpiralAbyssInfo info)
    {
        var assets = Shell.Assets;
        var targets = new List<(string? Url, Action<Bitmap> Apply)>();
        foreach (var rank in RevealRanks)
        {
            targets.Add((rank.Url, b => rank.Icon = b));
        }

        foreach (var row in CombatRows)
        {
            targets.Add((row.Url, b => row.Icon = b));
        }

        foreach (var floor in Floors)
        {
            foreach (var level in floor.Levels)
            {
                foreach (var avatar in level.UpperAvatars)
                {
                    targets.Add((avatar.Url, b => avatar.Icon = b));
                }

                foreach (var avatar in level.LowerAvatars)
                {
                    targets.Add((avatar.Url, b => avatar.Icon = b));
                }
            }
        }

        foreach (var (url, apply) in targets)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var bitmap = await assets.BitmapAsync(url).ConfigureAwait(true);
            if (bitmap is not null)
            {
                apply(bitmap);
            }
        }
    }
}


/// <summary>左侧一个统计周期（Starward <c>ListView_AbyssList</c> 的 <c>SpiralAbyssInfo</c>）。</summary>
public sealed partial class AbyssPeriodItem(SpiralAbyssInfo info) : ObservableObject
{
    public SpiralAbyssInfo Info { get; } = info;

    public string DateText => Info.StartTime.LocalDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public string TotalStarText => Info.TotalStar.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty]
    private bool _isSelected;
}


/// <summary>出战榜 / 阵容里的一格头像（稀有度底 + 头像 + 等级或次数）。</summary>
public sealed partial class AbyssAvatarItem(string? url, int rarity, int level, int value) : ObservableObject
{
    public string? Url { get; } = url;

    public int Rarity { get; } = rarity;

    public bool IsRank { get; } = value > 0 && level == 0;

    public string LevelText => IsRank ? "" : $"Lv.{level}";

    public string ValueText => IsRank ? string.Format(Localization.AppStrings.Instance["SpiralAbyss.Times"], value) : "";

    [ObservableProperty]
    private Bitmap? _icon;
}


/// <summary>战斗数据榜的一行（Starward 的 5 个 <c>FirstXxxRank</c>）。</summary>
public sealed partial class AbyssRankRow(string label, SpiralAbyssRank? rank, int index) : ObservableObject
{
    public string Label { get; } = label;

    public int Index { get; } = index;

    /// <summary>Starward 的 LayerOnAcrylicFill 隔行底色（第 2、4 行）。</summary>
    public bool IsAlternating => Index is 1 or 3;

    public string? Url { get; } = rank?.AvatarIcon;

    public int Rarity { get; } = rank?.Rarity ?? 0;

    public string ValueText => rank is null
        ? "—"
        : rank.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty]
    private Bitmap? _icon;
}


/// <summary>一层（Starward <c>SpiralAbyssFloor</c>）。</summary>
public sealed class AbyssFloorItem
{
    public AbyssFloorItem(SpiralAbyssFloor floor, Localization.AppStrings strings)
    {
        Title = string.Format(strings["SpiralAbyss.FloorX"], floor.Index);
        StarText = $"{floor.Star}/{floor.MaxStar}";
        Levels = floor.Levels?.Select(x => new AbyssLevelItem(x, strings)).ToList() ?? [];
    }

    public string Title { get; }

    public string StarText { get; }

    public List<AbyssLevelItem> Levels { get; }
}


/// <summary>一间（Starward <c>SpiralAbyssLevel</c>）。</summary>
public sealed class AbyssLevelItem
{
    public AbyssLevelItem(SpiralAbyssLevel level, Localization.AppStrings strings)
    {
        Title = string.Format(strings["SpiralAbyss.ChamberX"], level.Index);
        TimeText = level.Battles is { Count: > 0 }
            ? level.FirstBattleTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
            : "";
        Stars = level.Star;
        StarMarks = Enumerable.Range(0, Stars).ToArray();
        var battles = level.Battles ?? [];
        UpperAvatars = battles.Count > 0
            ? battles[0].Avatars?.Select(x => new AbyssAvatarItem(x.Icon, x.Rarity, x.Level, 0)).ToList() ?? []
            : [];
        LowerAvatars = battles.Count > 1
            ? battles[1].Avatars?.Select(x => new AbyssAvatarItem(x.Icon, x.Rarity, x.Level, 0)).ToList() ?? []
            : [];
    }

    public string Title { get; }

    public string TimeText { get; }

    public int Stars { get; }

    /// <summary>逐颗星星（XAML ItemsControl；Starward 用 Unicode 星字符循环）。</summary>
    public int[] StarMarks { get; }

    public List<AbyssAvatarItem> UpperAvatars { get; }

    public List<AbyssAvatarItem> LowerAvatars { get; }
}
