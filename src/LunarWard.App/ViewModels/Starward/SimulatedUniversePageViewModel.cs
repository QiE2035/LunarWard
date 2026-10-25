using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Services;
using LunarWard.App.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.StarRail.SimulatedUniverse;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 模拟宇宙页 VM（Starward <c>Features/GameRecord/StarRail/SimulatedUniversePage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 结构 = 左栏「刷新 + 可选「数据总览」（源码隐藏）+ 期次列表（每期开始日期）」
/// + 右侧「数据总览卡（技能树/奇物/祝福三列）→ 本期卡（通关次数 + 统计周期）
/// → 每期一条 Expander 记录（第几世界 + 难度罗马数字 + 最高积分 + 挑战时间 + 通关角色
/// + 祝福图标行；展开后是缓存角色 / 获得祝福（按命途分组，带进化图标）/ 获得奇物）」。
/// </para>
/// <para>
/// 数据来源差异：Starward 用 SQLite 缓存（先「获取明细」再读库，左栏列出全部历史）。
/// LunarWard 接口一次只返回「当期 + 上期」，因此左栏固定两项，切换不重新请求。
/// </para>
/// <para>
/// 素材差异：世界图标、祝福图标、奇物图标都是游戏内 PNG，本仓库无随包素材，
/// 统一用「圆底 + 字形」占位；<c>RarityToSRBg</c>（角色卡底）同样以纯色块近似。
/// </para>
/// </remarks>
public sealed partial class SimulatedUniversePageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static readonly SolidColorBrush Rank1Brush = new(Color.Parse("#686870"));

    private static readonly SolidColorBrush Rank2Brush = new(Color.Parse("#5379B5"));

    private static readonly SolidColorBrush Rank3Brush = new(Color.Parse("#BC9B6E"));

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    private SimulatedUniverseInfo? _info;

    private bool _initialized;


    public SimulatedUniversePageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _ = InitializeAsync();
    }


    /// <summary>左栏期次（当期 + 上期，Starward <c>RecordBasicList</c>）。</summary>
    public ObservableCollection<SimulatedUniversePeriodItem> PeriodList { get; } = [];

    [ObservableProperty]
    private SimulatedUniversePeriodItem? _selectedPeriod;

    partial void OnSelectedPeriodChanged(SimulatedUniversePeriodItem? value) => ApplyRecord(value?.Record);

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorText;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    [ObservableProperty]
    private SimulatedUniverseBasicStats? _basicInfo;

    public bool HasBasicInfo => BasicInfo is not null;

    [ObservableProperty]
    private SimulatedUniverseRecord? _currentRecord;

    public bool HasRecord => CurrentRecord is { HasData: true };

    public bool HasNoData => !HasRecord;

    public string UnlockedSkillPointsText => BasicInfo?.UnlockedSkillPoints.ToString(CultureInfo.InvariantCulture) ?? "";

    public string UnlockedMiracleNumText => BasicInfo?.UnlockedMiracleNum.ToString(CultureInfo.InvariantCulture) ?? "";

    public string UnlockedBuffNumText => BasicInfo?.UnlockedBuffNum.ToString(CultureInfo.InvariantCulture) ?? "";

    public string FinishCountText => CurrentRecord?.Basic?.FinishCount.ToString(CultureInfo.InvariantCulture) ?? "";

    public string PeriodText => CurrentRecord?.Basic is { } basic
        ? string.Format(
            CultureInfo.InvariantCulture,
            "{0} - {1}",
            basic.ScheduleBegin.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
            basic.ScheduleEnd.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture))
        : "";

    /// <summary>该期的通关记录（源码 <c>CurrentRecord.Records</c>）。</summary>
    public ObservableCollection<SimulatedUniverseItemViewModel> Items { get; } = [];


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
            ApplyDemo();
            return;
        }

        IsLoading = true;
        ErrorText = null;
        try
        {
            var info = await _client.GetSimulatedUniverseInfoAsync(_role, true).ConfigureAwait(true);
            _info = info;
            BasicInfo = info.BasicInfo;

            PeriodList.Clear();
            if (info.CurrentRecord is { Basic: not null } current)
            {
                PeriodList.Add(new SimulatedUniversePeriodItem(current, Strings));
            }

            if (info.LastRecord is { Basic: not null } last)
            {
                PeriodList.Add(new SimulatedUniversePeriodItem(last, Strings));
            }

            ShowEmpty = PeriodList.Count == 0;
            if (!ShowEmpty)
            {
                SelectedPeriod = PeriodList[0];
            }
        }
        catch (miHoYoApiException ex)
        {
            ErrorText = string.Format(Strings["SimulatedUniverse.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorText = string.Format(Strings["SimulatedUniverse.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["SimulatedUniverse.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }


    private void ApplyRecord(SimulatedUniverseRecord? record)
    {
        CurrentRecord = record;
        Items.Clear();
        if (record is { HasData: true, Records: { Count: > 0 } records })
        {
            foreach (var item in records)
            {
                Items.Add(new SimulatedUniverseItemViewModel(item, Strings));
            }

            _ = LoadImagesAsync();
        }

        OnPropertyChanged(nameof(HasBasicInfo));
        OnPropertyChanged(nameof(HasRecord));
        OnPropertyChanged(nameof(HasNoData));
        OnPropertyChanged(nameof(UnlockedSkillPointsText));
        OnPropertyChanged(nameof(UnlockedMiracleNumText));
        OnPropertyChanged(nameof(UnlockedBuffNumText));
        OnPropertyChanged(nameof(FinishCountText));
        OnPropertyChanged(nameof(PeriodText));
    }


    private async Task LoadImagesAsync()
    {
        var assets = Shell.Assets;
        foreach (var item in Items)
        {
            await item.LoadImagesAsync(assets).ConfigureAwait(true);
        }
    }


    /// <summary>罗马数字（源码 <c>SimulatedUniverseRomanNumberConverter</c>）。</summary>
    public static string RomanNumber(int value) => value switch
    {
        1 => "Ⅰ",
        2 => "Ⅱ",
        3 => "Ⅲ",
        4 => "Ⅳ",
        5 => "Ⅴ",
        6 => "Ⅵ",
        7 => "Ⅶ",
        8 => "Ⅷ",
        9 => "Ⅸ",
        10 => "Ⅹ",
        _ => "",
    };


    /// <summary>祝福稀有度底色（源码 <c>SimulatedUniverseBuffBgConverter</c>）。</summary>
    public static IBrush RankBrush(int rank) => rank switch
    {
        1 => Rank1Brush,
        2 => Rank2Brush,
        3 => Rank3Brush,
        _ => Brushes.Transparent,
    };


    /// <summary>
    /// 截图夹具（<c>LUNARWARD_TOOLBOX_DEMO=1</c>）：两期记录，当期两条（一条完整、一条只有头像），
    /// 祝福按命途分组并带进化标记，奇物六个。
    /// </summary>
    private void ApplyDemo()
    {
        var now = DateTimeOffset.Now;
        BasicInfo = new SimulatedUniverseBasicStats
        {
            UnlockedSkillPoints = 47,
            UnlockedMiracleNum = 112,
            UnlockedBuffNum = 638,
        };

        var basic = new SimulatedUniverseRecordBasic
        {
            ScheduleId = 1,
            ScheduleBegin = new DateTime(now.Year, now.Month, 1),
            ScheduleEnd = new DateTime(now.Year, now.Month, 15),
            FinishCount = 3,
        };
        var record = new SimulatedUniverseRecord
        {
            Basic = basic,
            HasData = true,
            Records =
            [
                BuildDemoItem("寰宇蝗灾", progress: 3, difficulty: 3, score: 12800, withBuffs: true),
                BuildDemoItem("模拟宇宙：黄金与机械", progress: 1, difficulty: 4, score: 8600, withBuffs: false),
            ],
        };
        var lastBasic = new SimulatedUniverseRecordBasic
        {
            ScheduleId = 2,
            ScheduleBegin = new DateTime(now.Year, now.Month, 16),
            ScheduleEnd = new DateTime(now.Year, now.Month, 28),
            FinishCount = 1,
        };
        var last = new SimulatedUniverseRecord
        {
            Basic = lastBasic,
            HasData = true,
            Records = [BuildDemoItem("寰宇蝗灾", progress: 2, difficulty: 2, score: 7200, withBuffs: true)],
        };

        _info = new SimulatedUniverseInfo { BasicInfo = BasicInfo, CurrentRecord = record, LastRecord = last };

        PeriodList.Clear();
        PeriodList.Add(new SimulatedUniversePeriodItem(record, Strings));
        PeriodList.Add(new SimulatedUniversePeriodItem(last, Strings));
        ShowEmpty = false;
        SelectedPeriod = PeriodList[0];
    }


    private static SimulatedUniverseRecordItem BuildDemoItem(string name, int progress, int difficulty, int score, bool withBuffs)
    {
        var item = new SimulatedUniverseRecordItem
        {
            Name = name,
            Progress = progress,
            Difficulty = difficulty,
            Score = score,
            FinishTime = DateTime.Now.AddDays(-2),
            FinalLineup =
            [
                new SimulatedUniverseAvatar { Id = 1201, Level = 80, Icon = "", Rarity = 5 },
                new SimulatedUniverseAvatar { Id = 1202, Level = 80, Icon = "", Rarity = 4 },
                new SimulatedUniverseAvatar { Id = 1203, Level = 80, Icon = "", Rarity = 4 },
                new SimulatedUniverseAvatar { Id = 1204, Level = 80, Icon = "", Rarity = 5 },
            ],
            CachedAvatars = withBuffs
                ?
                [
                    new SimulatedUniverseAvatar { Id = 1101, Level = 80, Icon = "", Rarity = 5 },
                    new SimulatedUniverseAvatar { Id = 1102, Level = 80, Icon = "", Rarity = 4 },
                ]
                : [],
        };

        if (withBuffs)
        {
            item.BuffTypeList =
            [
                new SimulatedUniverseBuffType { Id = 1, Name = "存护", Cnt = 8 },
                new SimulatedUniverseBuffType { Id = 2, Name = "巡猎", Cnt = 6 },
                new SimulatedUniverseBuffType { Id = 3, Name = "丰饶", Cnt = 5 },
            ];
            item.Buffs =
            [
                new SimulatedUniverseBuff
                {
                    BuffType = new SimulatedUniverseBuffType { Id = 1, Name = "存护" },
                    Items =
                    [
                        new SimulatedUniverseBuffItem { Name = "反物质军团", Rank = 3, IsEvoluted = true },
                        new SimulatedUniverseBuffItem { Name = "坚信不疑", Rank = 2, IsEvoluted = false },
                    ],
                },
                new SimulatedUniverseBuff
                {
                    BuffType = new SimulatedUniverseBuffType { Id = 2, Name = "巡猎" },
                    Items = [new SimulatedUniverseBuffItem { Name = "雷霆战车", Rank = 1, IsEvoluted = false }],
                },
            ];
        }

        item.Miracles = Enumerable.Range(1, 6)
            .Select(i => new SimulatedUniverseMiracleItem { Id = i, Name = $"奇物 {i}", Icon = "" })
            .ToList();

        return item;
    }
}


/// <summary>左栏的一期（Starward <c>ListView_SimulatedUniverse</c> 行：开始日期）。</summary>
public sealed class SimulatedUniversePeriodItem
{
    public SimulatedUniversePeriodItem(SimulatedUniverseRecord record, Localization.AppStrings strings)
    {
        Record = record;
        Strings = strings;
        DateText = record.Basic?.ScheduleBegin.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
    }

    public SimulatedUniverseRecord Record { get; }

    public Localization.AppStrings Strings { get; }

    public string DateText { get; }
}


/// <summary>一条通关记录（源码 <c>SimulatedUniverseRecordItem</c> 的视图模型）。</summary>
public sealed partial class SimulatedUniverseItemViewModel : ObservableObject
{
    private readonly SimulatedUniverseRecordItem _item;

    public SimulatedUniverseItemViewModel(SimulatedUniverseRecordItem item, Localization.AppStrings strings)
    {
        _item = item;
        Strings = strings;
        Name = item.Name;
        DifficultyText = SimulatedUniversePageViewModel.RomanNumber(item.Difficulty);
        ScoreText = item.Score.ToString(CultureInfo.InvariantCulture);
        FinishTimeText = item.FinishTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        Lineup = new(item.FinalLineup?.Select(x => new SimulatedUniverseAvatarViewModel(x)) ?? []);
        CachedAvatars = new(item.CachedAvatars?.Select(x => new SimulatedUniverseAvatarViewModel(x)) ?? []);
        BuffTypeList = new(item.BuffTypeList?.Select(x => new SimulatedUniverseBuffTypeViewModel(x)) ?? []);
        Buffs = new(item.Buffs?.Select(x => new SimulatedUniverseBuffViewModel(x)) ?? []);
        Miracles = new(item.Miracles?.Select(x => new SimulatedUniverseMiracleViewModel(x)) ?? []);
    }

    public Localization.AppStrings Strings { get; }

    public string Name { get; }

    public string DifficultyText { get; }

    public string ScoreText { get; }

    public string FinishTimeText { get; }

    public bool HasCachedAvatars => CachedAvatars.Count > 0;

    public ObservableCollection<SimulatedUniverseAvatarViewModel> Lineup { get; }

    public ObservableCollection<SimulatedUniverseAvatarViewModel> CachedAvatars { get; }

    public ObservableCollection<SimulatedUniverseBuffTypeViewModel> BuffTypeList { get; }

    public ObservableCollection<SimulatedUniverseBuffViewModel> Buffs { get; }

    public ObservableCollection<SimulatedUniverseMiracleViewModel> Miracles { get; }

    public async Task LoadImagesAsync(StarwardAssets assets)
    {
        foreach (var avatar in Lineup)
        {
            await avatar.LoadAsync(assets).ConfigureAwait(true);
        }

        foreach (var avatar in CachedAvatars)
        {
            await avatar.LoadAsync(assets).ConfigureAwait(true);
        }

        foreach (var miracle in Miracles)
        {
            await miracle.LoadAsync(assets).ConfigureAwait(true);
        }
    }
}


/// <summary>角色卡（源码 72×84，稀有度底 + 头像 + Lv 条）。</summary>
public sealed partial class SimulatedUniverseAvatarViewModel : ObservableObject
{
    private readonly string? _url;

    public SimulatedUniverseAvatarViewModel(SimulatedUniverseAvatar avatar)
    {
        _url = avatar.Icon;
        LevelText = $"Lv.{avatar.Level}";
        BackgroundBrush = new SolidColorBrush(avatar.Rarity >= 5 ? Color.Parse("#FFC29B62") : Color.Parse("#FFA15EBC"));
    }

    public string LevelText { get; }

    public IBrush BackgroundBrush { get; }

    [ObservableProperty]
    private Bitmap? _icon;

    public async Task LoadAsync(StarwardAssets assets)
    {
        Icon = await assets.BitmapAsync(_url).ConfigureAwait(true);
    }
}


/// <summary>命途图标（源码 48×48 圆底 + 右下数量角标）。</summary>
public sealed class SimulatedUniverseBuffTypeViewModel
{
    public SimulatedUniverseBuffTypeViewModel(SimulatedUniverseBuffType type)
    {
        Name = type.Name;
        CountText = type.Cnt.ToString(CultureInfo.InvariantCulture);
    }

    public string Name { get; }

    public string CountText { get; }
}


/// <summary>命途分组（24 图标 + 名称 + 祝福条）。</summary>
public sealed class SimulatedUniverseBuffViewModel
{
    public SimulatedUniverseBuffViewModel(SimulatedUniverseBuff buff)
    {
        TypeName = buff.BuffType?.Name ?? "";
        Items = new(buff.Items?.Select(x => new SimulatedUniverseBuffItemViewModel(x)) ?? []);
    }

    public string TypeName { get; }

    public ObservableCollection<SimulatedUniverseBuffItemViewModel> Items { get; }
}


/// <summary>单条祝福（源码按 <c>Rank</c> 上底色，进化时右侧加 EB11 图标）。</summary>
public sealed class SimulatedUniverseBuffItemViewModel
{
    public SimulatedUniverseBuffItemViewModel(SimulatedUniverseBuffItem item)
    {
        Name = item.Name;
        IsEvoluted = item.IsEvoluted;
        Background = SimulatedUniversePageViewModel.RankBrush(item.Rank);
    }

    public string Name { get; }

    public bool IsEvoluted { get; }

    public IBrush Background { get; }
}


/// <summary>奇物（32×32 圆图 + ToolTip 名称）。</summary>
public sealed partial class SimulatedUniverseMiracleViewModel : ObservableObject
{
    private readonly string? _url;

    public SimulatedUniverseMiracleViewModel(SimulatedUniverseMiracleItem miracle)
    {
        _url = miracle.Icon;
        Name = miracle.Name;
    }

    public string Name { get; }

    [ObservableProperty]
    private Bitmap? _icon;

    public async Task LoadAsync(StarwardAssets assets)
    {
        Icon = await assets.BitmapAsync(_url).ConfigureAwait(true);
    }
}
