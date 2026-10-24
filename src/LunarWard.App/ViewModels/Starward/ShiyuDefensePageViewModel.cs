using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.ZZZ;
using LunarWard.Core.GameRecord.ZZZ.ShiyuDefense;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 式舆防卫战页 VM（Starward <c>Features/GameRecord/ZZZ/ShiyuDefensePage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 左侧是「当期 / 上期」两个赛期（Starward 从 SQLite 读缓存列表；LunarWard 每次进页
/// 直接调 <c>shiyu_defense?schedule=1/2</c>）。右侧按 <c>hadal_ver</c> 显示 v1 或 v2 布局：
/// v1 = 出战统计（S/A/B 次数、最高防线、4-7 层通关时长）+ 逐防线卡片（防线效果 / 评级 /
/// 通关时间 / 两支队伍的角色、邦布、敌人详情）；v2 = 第五防线概要卡 + 第五/第四防线节点卡。
/// </para>
/// <para>
/// 素材：角色 / 邦布 / 敌人图标来自米游社 CDN，用 <see cref="Services.StarwardAssets.BitmapAsync"/>
/// 下载；S/A/B 评级图、属性弱点图标不在 LunarWard 打包资源里，用同色文字圆章 + 属性字形占位。
/// </para>
/// </remarks>
public sealed partial class ShiyuDefensePageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static readonly Regex RichTextTag = new("<[^>]+>", RegexOptions.Compiled);

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    private readonly ShiyuLabels _labels;

    public ShiyuDefensePageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _labels = new ShiyuLabels(
            Strings["ShiyuDefense.Team1"],
            Strings["ShiyuDefense.Team2"],
            Strings["ShiyuDefense.FrontierEffects"],
            Strings["ShiyuDefense.Weakness"],
            Strings["ShiyuDefense.Resistance"],
            Strings["ShiyuDefense.EnemyDetails"],
            Strings["ShiyuDefense.ClearedAt"],
            Strings["ShiyuDefense.ClearTimeLabel"]);
        _ = InitializeAsync();
    }


    /// <summary>左侧赛期列表（Starward <c>ListView_ShiyuDefense</c>）。</summary>
    public ObservableCollection<ShiyuDefensePeriodItem> DefenseList { get; } = [];

    [ObservableProperty]
    private ShiyuDefensePeriodItem? _currentDefense;

    partial void OnCurrentDefenseChanged(ShiyuDefensePeriodItem? value)
    {
        ApplyCurrent(value);
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _showEmpty = true;

    [ObservableProperty]
    private string? _errorText;


    public string PeriodText => CurrentDefense?.BeginTimeText is { } period && !string.IsNullOrEmpty(period)
        ? string.Format(Strings["SpiralAbyss.PeriodRange"], CurrentDefense.DateText, CurrentDefense.EndDateText)
        : "";

    public bool ShowV1 => CurrentDefense?.IsV1 == true;

    public bool ShowV2 => CurrentDefense?.IsV2 == true;


    partial void OnIsBusyChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();

    private bool CanRefresh() => !IsBusy;


    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorText = null;
        DefenseList.Clear();
        ShowEmpty = true;

        try
        {
            var items = new List<ShiyuDefensePeriodItem>();
            Exception? firstError = null;

            foreach (var schedule in new[] { 1, 2 })
            {
                try
                {
                    var wrapper = await _client.GetShiyuDefenseInfoAsync(_role, schedule, default).ConfigureAwait(true);
                    var item = ShiyuDefensePeriodItem.TryCreate(wrapper, _labels);
                    if (item is not null)
                    {
                        items.Add(item);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    firstError ??= ex;
                }
            }

            foreach (var item in items)
            {
                DefenseList.Add(item);
            }

            ShowEmpty = DefenseList.Count == 0;
            CurrentDefense = DefenseList.FirstOrDefault();

            if (ShowEmpty && firstError is not null)
            {
                ErrorText = string.Format(Strings["ShiyuDefense.LoadFailed"], firstError.Message);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorText = string.Format(Strings["ShiyuDefense.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        finally
        {
            IsBusy = false;
        }
    }


    /// <summary>首次进入时拉取当期 + 上期（Starward <c>OnNavigatedTo</c>）。</summary>
    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("LUNARWARD_TOOLBOX_DEMO") == "1")
        {
            ApplyDemo();
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
    }


    private void ApplyCurrent(ShiyuDefensePeriodItem? item)
    {
        if (item is null)
        {
            ShowEmpty = true;
            return;
        }

        ShowEmpty = false;
        OnPropertyChanged(nameof(PeriodText));
        OnPropertyChanged(nameof(ShowV1));
        OnPropertyChanged(nameof(ShowV2));
        _ = LoadIconsAsync();
    }


    private async Task LoadIconsAsync()
    {
        var item = CurrentDefense;
        if (item is null)
        {
            return;
        }

        var assets = Shell.Assets;
        foreach (var avatar in EnumerateAvatars(item))
        {
            if (!string.IsNullOrWhiteSpace(avatar.Url))
            {
                avatar.Icon = await assets.BitmapAsync(avatar.Url).ConfigureAwait(true);
            }
        }

        foreach (var buddy in EnumerateBuddies(item))
        {
            if (!string.IsNullOrWhiteSpace(buddy.Url))
            {
                buddy.Icon = await assets.BitmapAsync(buddy.Url).ConfigureAwait(true);
            }
        }

        foreach (var challenge in EnumerateFifthChallenges(item))
        {
            if (!string.IsNullOrWhiteSpace(challenge.MonsterPicUrl))
            {
                challenge.MonsterPic = await assets.BitmapAsync(challenge.MonsterPicUrl).ConfigureAwait(true);
            }
        }

        foreach (var monster in EnumerateMonsters(item))
        {
            if (!string.IsNullOrWhiteSpace(monster.BgIconUrl))
            {
                monster.BgIcon = await assets.BitmapAsync(monster.BgIconUrl).ConfigureAwait(true);
            }

            if (!string.IsNullOrWhiteSpace(monster.IconUrl))
            {
                monster.Icon = await assets.BitmapAsync(monster.IconUrl).ConfigureAwait(true);
            }

            if (!string.IsNullOrWhiteSpace(monster.RaceIconUrl))
            {
                monster.RaceIcon = await assets.BitmapAsync(monster.RaceIconUrl).ConfigureAwait(true);
            }
        }
    }


    private static IEnumerable<ShiyuAvatarItem> EnumerateAvatars(ShiyuDefensePeriodItem item)
    {
        if (item.V1 is { } v1)
        {
            foreach (var floor in v1.Floors)
            {
                foreach (var node in new[] { floor.Node1, floor.Node2 })
                {
                    foreach (var avatar in node?.Avatars ?? [])
                    {
                        yield return avatar;
                    }
                }
            }
        }

        if (item.V2 is { } v2)
        {
            foreach (var challenge in v2.Fifth.Concat(v2.Fourth))
            {
                foreach (var avatar in challenge.Avatars)
                {
                    yield return avatar;
                }
            }
        }
    }


    private static IEnumerable<ShiyuBuddyItem> EnumerateBuddies(ShiyuDefensePeriodItem item)
    {
        if (item.V1 is { } v1)
        {
            foreach (var floor in v1.Floors)
            {
                if (floor.Node1?.Buddy is { } b1)
                {
                    yield return b1;
                }

                if (floor.Node2?.Buddy is { } b2)
                {
                    yield return b2;
                }
            }
        }

        if (item.V2 is { } v2)
        {
            foreach (var challenge in v2.Fifth.Concat(v2.Fourth))
            {
                if (challenge.Buddy is { } buddy)
                {
                    yield return buddy;
                }
            }
        }
    }


    private static IEnumerable<ShiyuMonsterItem> EnumerateMonsters(ShiyuDefensePeriodItem item)
    {
        if (item.V1 is not { } v1)
        {
            yield break;
        }

        foreach (var floor in v1.Floors)
        {
            foreach (var node in new[] { floor.Node1, floor.Node2 })
            {
                foreach (var monster in node?.Monsters ?? [])
                {
                    yield return monster;
                }
            }
        }
    }


    private static IEnumerable<ShiyuV2ChallengeItem> EnumerateFifthChallenges(ShiyuDefensePeriodItem item)
        => item.V2?.Fifth ?? [];


    /// <summary>演示夹具：无 cookie 时用固定数据渲染两种版本。</summary>
    private void ApplyDemo()
    {
        DefenseList.Clear();
        DefenseList.Add(ShiyuDefensePeriodItem.TryCreate(ShiyuDefenseDemo.CreateV2Wrapper(), _labels)!);
        DefenseList.Add(ShiyuDefensePeriodItem.TryCreate(ShiyuDefenseDemo.CreateV1Wrapper(), _labels)!);
        ShowEmpty = false;
        CurrentDefense = DefenseList[Environment.GetEnvironmentVariable("LUNARWARD_SHIYU_V1") == "1" ? 1 : 0];
    }


    public static IBrush RatingBrush(string? rating) => rating switch
    {
        "S" => new SolidColorBrush(Color.Parse("#FFB13F")),
        "A" => new SolidColorBrush(Color.Parse("#B57EDC")),
        "B" => new SolidColorBrush(Color.Parse("#7FB3FF")),
        _ => new SolidColorBrush(Color.Parse("#9AA0A6")),
    };


    public static IBrush RankPercentBrush(int value) => value switch
    {
        <= 100 => new SolidColorBrush(Color.Parse("#FFB13F")),
        <= 200 => new SolidColorBrush(Color.Parse("#C99BFF")),
        <= 500 => new SolidColorBrush(Color.Parse("#7FB3FF")),
        <= 3000 => new SolidColorBrush(Color.Parse("#6FCF97")),
        _ => new SolidColorBrush(Color.Parse("#9AA0A6")),
    };


    public static string PerformancesTime(int seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Minutes}m {ts.Seconds:D2}s";
    }


    public static string RankPercentText(int value)
    {
        int d = value / 100;
        int p = value % 100;
        return $"{d}.{p:D2}%";
    }


    public static string CleanRichText(string? text) => RichTextTag.Replace(text ?? "", "").Trim();
}


/// <summary>页面内联文案（从 <see cref="ShiyuDefensePageViewModel.Strings"/> 取，避免模板里绑页面）。</summary>
public sealed record ShiyuLabels(string Team1, string Team2, string Effects, string Weakness, string Resistance, string EnemyDetails, string ClearedAt, string ClearTime);


/// <summary>左侧一个赛期（Starward <c>ListView_ShiyuDefense</c> 的 <c>ShiyuDefenseInfo</c> / v2）。</summary>
public sealed partial class ShiyuDefensePeriodItem : ObservableObject
{
    private ShiyuDefensePeriodItem(ShiyuDefenseInfoBase info, ShiyuLabels labels)
    {
        if (info is ShiyuDefenseInfo v1)
        {
            V1 = new ShiyuDefenseV1Item(v1, labels);
            DateText = v1.BeginTime.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            EndDateText = v1.EndTime.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            RatingLetter = string.IsNullOrEmpty(v1.MaxRating) ? "?" : v1.MaxRating;
            TimesText = $"x{v1.MaxRatingTimes}";
        }
        else if (info is ShiyuDefenseInfoV2 v2)
        {
            V2 = new ShiyuDefenseV2Item(v2, labels);
            DateText = v2.BeginTime.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            EndDateText = v2.EndTime.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            RatingLetter = string.IsNullOrEmpty(v2.MaxRating) ? "?" : v2.MaxRating;
            ScoreText = v2.V2Score.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            throw new ArgumentException("Unknown ShiyuDefenseInfo type.", nameof(info));
        }
    }

    public static ShiyuDefensePeriodItem? TryCreate(ShiyuDefenseWrapper wrapper, ShiyuLabels labels)
    {
        if (wrapper.InfoV1 is { HasData: true })
        {
            return new ShiyuDefensePeriodItem(wrapper.InfoV1, labels);
        }

        if (wrapper.InfoV2 is { HasData: true })
        {
            return new ShiyuDefensePeriodItem(wrapper.InfoV2, labels);
        }

        return null;
    }

    public bool IsV1 => V1 is not null;

    public bool IsV2 => V2 is not null;

    public ShiyuDefenseV1Item? V1 { get; }

    public ShiyuDefenseV2Item? V2 { get; }

    public string DateText { get; }

    public string EndDateText { get; }

    public string BeginTimeText => DateText;

    public string RatingLetter { get; }

    public IBrush RatingBrush => ShiyuDefensePageViewModel.RatingBrush(RatingLetter);

    public string TimesText { get; } = "";

    public string ScoreText { get; } = "";

    public bool HasTimes => !string.IsNullOrEmpty(TimesText);

    public bool HasScore => !string.IsNullOrEmpty(ScoreText);
}


/// <summary>v1 详情（出战统计 + 逐防线卡片）。</summary>
public sealed partial class ShiyuDefenseV1Item : ObservableObject
{
    public ShiyuDefenseV1Item(ShiyuDefenseInfo info, ShiyuLabels labels)
    {
        Info = info;
        Labels = labels;
        Floors = info.AllFloorDetail?.Select(x => new ShiyuFloorItem(x, labels)).ToList() ?? [];
    }

    public ShiyuLabels Labels { get; }

    public ShiyuDefenseInfo Info { get; }

    public List<ShiyuFloorItem> Floors { get; }

    public int STimes => Info.RatingSTimes;

    public int ATimes => Info.RatingATimes;

    public int BTimes => Info.RatingBTimes;

    public string MaxLayerText => Info.MaxLayer.ToString(CultureInfo.InvariantCulture);

    public string BattleTime47Text => ShiyuDefensePageViewModel.PerformancesTime(Info.BattleTime47);

}


/// <summary>v1 一条防线。</summary>
public sealed partial class ShiyuFloorItem : ObservableObject
{
    public ShiyuFloorItem(ShiyuDefenseFloorDetail floor, ShiyuLabels labels)
    {
        Floor = floor;
        Labels = labels;
        Buffs = floor.Buffs?.Select(x => new ShiyuBuffItem(x)).ToList() ?? [];
        Node1 = floor.Node1 is null ? null : new ShiyuNodeItem(floor.Node1, labels);
        Node2 = floor.Node2 is null ? null : new ShiyuNodeItem(floor.Node2, labels);
    }

    public ShiyuLabels Labels { get; }

    public string Team1Label => Labels.Team1;

    public string Team2Label => Labels.Team2;

    public string EffectsLabel => Labels.Effects;

    public string EnemyLabel => Labels.EnemyDetails;

    public string ClearTimeLabel => Labels.ClearTime;

    public ShiyuDefenseFloorDetail Floor { get; }

    public List<ShiyuBuffItem> Buffs { get; }

    public bool HasBuffs => Buffs.Count > 0;

    public string ZoneName => Floor.ZoneName ?? "";

    public string RatingLetter => string.IsNullOrEmpty(Floor.Rating) ? "?" : Floor.Rating;

    public IBrush RatingBrush => ShiyuDefensePageViewModel.RatingBrush(Floor.Rating);

    public string ChallengeTimeText => Floor.FloorChallengeTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public ShiyuNodeItem? Node1 { get; }

    public ShiyuNodeItem? Node2 { get; }
}


/// <summary>一支队伍（Starward <c>ShiyuDefenseNode</c>）。</summary>
public sealed partial class ShiyuNodeItem : ObservableObject
{
    public ShiyuNodeItem(ShiyuDefenseNode node, ShiyuLabels labels)
    {
        Node = node;
        Avatars = node.Avatars?.Select(x => new ShiyuAvatarItem(x)).ToList() ?? [];
        Buddy = node.Buddy is null ? null : new ShiyuBuddyItem(node.Buddy);
        Monsters = node.MonsterInfo?.List?.Select(x => new ShiyuMonsterItem(x, labels)).ToList() ?? [];
        ClearTimeText = ShiyuDefensePageViewModel.PerformancesTime(node.BattleTime);
        ClearTimeLabel = labels.ClearTime;
    }

    public ShiyuDefenseNode Node { get; }

    public List<ShiyuAvatarItem> Avatars { get; }

    public ShiyuBuddyItem? Buddy { get; }

    public List<ShiyuMonsterItem> Monsters { get; }

    public bool HasMonsters => Monsters.Count > 0;

    public string ClearTimeText { get; }

    public string ClearTimeLabel { get; }
}


/// <summary>v2 详情（第五防线概要 + 第五/第四防线节点）。</summary>
public sealed partial class ShiyuDefenseV2Item : ObservableObject
{
    public ShiyuDefenseV2Item(ShiyuDefenseInfoV2 info, ShiyuLabels labels)
    {
        Info = info;
        Labels = labels;
        Fifth = info.FifthLayerDetail?.LayerChallenges?.Select(x => new ShiyuV2ChallengeItem(x, labels)).ToList() ?? [];
        Fourth = info.FourthLayerDetail?.LayerChallenges?.Select(x => new ShiyuV2ChallengeItem(x, labels)).ToList() ?? [];
        FourthBuff = info.FourthLayerDetail?.Buff is { } buff ? new ShiyuBuffItem(buff) : null;
        var fourthTime = info.FourthLayerDetail?.ChallengeTime ?? DateTime.MinValue;
        if (fourthTime == DateTime.MinValue)
        {
            fourthTime = info.Brief?.ChallengeTime ?? DateTime.MinValue;
        }

        FourthChallengeTimeText = fourthTime != DateTime.MinValue
            ? fourthTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : "";
        FourthRatingLetter = string.IsNullOrEmpty(info.FourthLayerDetail?.Rating) ? "" : info.FourthLayerDetail.Rating;
    }

    public ShiyuLabels Labels { get; }

    public ShiyuDefenseInfoV2 Info { get; }

    public List<ShiyuV2ChallengeItem> Fifth { get; }

    public List<ShiyuV2ChallengeItem> Fourth { get; }

    public ShiyuBuffItem? FourthBuff { get; }

    public bool HasFourthBuff => FourthBuff is not null;

    public string FourthChallengeTimeText { get; }

    public bool HasFourthChallengeTime => !string.IsNullOrEmpty(FourthChallengeTimeText);

    public string FourthRatingLetter { get; }

    public string BriefScoreText => Info.Brief?.Score.ToString(CultureInfo.InvariantCulture) ?? "0";

    public string RankPercentText => ShiyuDefensePageViewModel.RankPercentText(Info.Brief?.RankPercent ?? 0);

    public IBrush RankPercentBrush => ShiyuDefensePageViewModel.RankPercentBrush(Info.Brief?.RankPercent ?? 0);

    public string RatingLetter => Info.Brief?.Rating ?? "";

    public IBrush RatingBrush => ShiyuDefensePageViewModel.RatingBrush(RatingLetter);

    public string ChallengeTimeText => Info.Brief?.ChallengeTime is { } time && time != DateTime.MinValue
        ? time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        : "";

    public bool HasChallengeTime => !string.IsNullOrEmpty(ChallengeTimeText);

    public bool HasClearedAt => HasChallengeTime;

    public string BattleTimeText => Info.Brief?.BattleTime > 0
        ? ShiyuDefensePageViewModel.PerformancesTime(Info.Brief.BattleTime)
        : "";

    public bool HasBattleTime => !string.IsNullOrEmpty(BattleTimeText);
}


/// <summary>v2 一个防线节点。</summary>
public sealed partial class ShiyuV2ChallengeItem : ObservableObject
{
    public ShiyuV2ChallengeItem(ShiyuDefenseV2FourthLayerChallengeInfo challenge, ShiyuLabels labels)
    {
        Challenge = challenge;
        ClearTimeLabel = labels.ClearTime;
        ClearedAtLabel = labels.ClearedAt;
        EffectsLabel = labels.Effects;
        Avatars = challenge.AvatarList?.Select(x => new ShiyuAvatarItem(x)).ToList() ?? [];
        Buddy = challenge.Buddy is null ? null : new ShiyuBuddyItem(challenge.Buddy);
        if (challenge is ShiyuDefenseV2FifthLayerChallengeInfo fifth)
        {
            Buff = fifth.Buff is null ? null : new ShiyuBuffItem(fifth.Buff);
            ScoreText = fifth.Score.ToString(CultureInfo.InvariantCulture);
            RatingLetter = fifth.Rating ?? "";
            ChallengeTimeText = fifth.ChallengeTime != DateTime.MinValue
                ? fifth.ChallengeTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : "";
            MonsterPicUrl = fifth.MonsterPic;
        }

        BattleTimeText = challenge.BattleTime > 0 ? ShiyuDefensePageViewModel.PerformancesTime(challenge.BattleTime) : "";
    }

    public string ClearTimeLabel { get; }

    public string ClearedAtLabel { get; }

    public string EffectsLabel { get; }

    public string? MonsterPicUrl { get; }

    public ShiyuDefenseV2FourthLayerChallengeInfo Challenge { get; }

    public List<ShiyuAvatarItem> Avatars { get; }

    public ShiyuBuddyItem? Buddy { get; }

    public ShiyuBuffItem? Buff { get; }

    public bool HasBuff => Buff is not null;

    public string ScoreText { get; } = "";

    public bool HasScore => !string.IsNullOrEmpty(ScoreText);

    public string RatingLetter { get; } = "";

    public IBrush RatingBrush => ShiyuDefensePageViewModel.RatingBrush(RatingLetter);

    public bool HasRating => !string.IsNullOrEmpty(RatingLetter);

    public string ChallengeTimeText { get; } = "";

    public bool HasChallengeTime => !string.IsNullOrEmpty(ChallengeTimeText);

    public bool HasClearedAt => HasChallengeTime;

    public string BattleTimeText { get; } = "";

    public bool HasBattleTime => !string.IsNullOrEmpty(BattleTimeText);

    [ObservableProperty]
    private Bitmap? _monsterPic;
}


/// <summary>角色格子（Starward 76×94 卡片）。</summary>
public sealed partial class ShiyuAvatarItem : ObservableObject
{
    public ShiyuAvatarItem(ZZZAvatar avatar)
    {
        Url = avatar.RoleSquareUrl;
        LevelText = $"Lv.{avatar.Level}";
        RarityText = string.IsNullOrEmpty(avatar.Rarity) ? "?" : avatar.Rarity;
        RarityBrush = RarityText == "S"
            ? new SolidColorBrush(Color.Parse("#FFB13F"))
            : new SolidColorBrush(Color.Parse("#B57EDC"));
        HasRank = avatar.Rank > 0;
        RankText = avatar.Rank.ToString(CultureInfo.InvariantCulture);
    }

    public string? Url { get; }

    public string LevelText { get; }

    public string RarityText { get; }

    public IBrush RarityBrush { get; }

    public bool HasRank { get; }

    public string RankText { get; }

    [ObservableProperty]
    private Bitmap? _icon;
}


/// <summary>邦布格子（Starward 58×78 卡片）。</summary>
public sealed partial class ShiyuBuddyItem : ObservableObject
{
    public ShiyuBuddyItem(ZZZBuddy buddy)
    {
        Url = buddy.RectangleUrl;
        LevelText = $"Lv.{buddy.Level}";
        RarityText = string.IsNullOrEmpty(buddy.Rarity) ? "?" : buddy.Rarity;
        RarityBrush = RarityText == "S"
            ? new SolidColorBrush(Color.Parse("#FFB13F"))
            : new SolidColorBrush(Color.Parse("#B57EDC"));
    }

    public string? Url { get; }

    public string LevelText { get; }

    public string RarityText { get; }

    public IBrush RarityBrush { get; }

    [ObservableProperty]
    private Bitmap? _icon;
}


/// <summary>敌人（Starward <c>ShiyuDefenseMonster</c>），弱点/抗性用属性字形占位。</summary>
public sealed partial class ShiyuMonsterItem : ObservableObject
{
    public ShiyuMonsterItem(ShiyuDefenseMonster monster, ShiyuLabels labels)
    {
        Monster = monster;
        WeaknessLabel = labels.Weakness;
        ResistanceLabel = labels.Resistance;
        Name = monster.Name ?? "";
        Weakness = BuildChips(1, monster);
        Resistance = BuildChips(-1, monster);
    }

    public ShiyuDefenseMonster Monster { get; }

    public string WeaknessLabel { get; }

    public string ResistanceLabel { get; }

    public string Name { get; }

    public string? BgIconUrl => Monster.BgIcon;

    public string? IconUrl => Monster.IconUrl;

    public string? RaceIconUrl => Monster.RaceIcon;

    public List<ShiyuElementChip> Weakness { get; }

    public List<ShiyuElementChip> Resistance { get; }

    public bool HasWeakness => Weakness.Count > 0;

    public bool HasResistance => Resistance.Count > 0;

    [ObservableProperty]
    private Bitmap? _bgIcon;

    [ObservableProperty]
    private Bitmap? _icon;

    [ObservableProperty]
    private Bitmap? _raceIcon;


    private static List<ShiyuElementChip> BuildChips(int wanted, ShiyuDefenseMonster monster)
    {
        var chips = new List<ShiyuElementChip>();
        Add(chips, monster.IceWeakness, wanted, "冰", "#7FE4FF");
        Add(chips, monster.FireWeakness, wanted, "火", "#FF7A6B");
        Add(chips, monster.ElecWeakness, wanted, "电", "#B47AFF");
        Add(chips, monster.EtherWeakness, wanted, "以太", "#FF7AD9");
        Add(chips, monster.PhysicsWeakness, wanted, "物", "#FFD27A");
        return chips;
    }

    private static void Add(List<ShiyuElementChip> chips, int value, int wanted, string label, string color)
    {
        if (value == wanted)
        {
            chips.Add(new ShiyuElementChip(label, color));
        }
    }
}


/// <summary>属性标签（弱点 / 抗性）。</summary>
public sealed partial class ShiyuElementChip(string label, string color) : ObservableObject
{
    public string Label { get; } = label;

    public IBrush Brush { get; } = new SolidColorBrush(Color.Parse(color));
}


/// <summary>防线效果 / 增益。</summary>
public sealed partial class ShiyuBuffItem : ObservableObject
{
    public ShiyuBuffItem(ShiyuDefenseBuff buff)
    {
        Title = buff.Title ?? "";
        Text = ShiyuDefensePageViewModel.CleanRichText(buff.Text);
    }

    public string Title { get; }

    public string Text { get; }
}


/// <summary>演示夹具（Starward 同款数据形状）。</summary>
public static class ShiyuDefenseDemo
{
    private static readonly DateTime Now = DateTime.Now;

    private static ZZZAvatar Avatar(int id, string rarity, int level, int rank, string? url = null) => new()
    {
        Id = id,
        Level = level,
        Rarity = rarity,
        Rank = rank,
        RoleSquareUrl = url ?? $"https://act-webstatic.mihoyo.com/game_record/zzz/avatar/{id}.png",
    };

    private static ZZZBuddy Buddy(int id, string rarity, int level) => new()
    {
        Id = id,
        Level = level,
        Rarity = rarity,
        RectangleUrl = $"https://act-webstatic.mihoyo.com/game_record/zzz/buddy/{id}.png",
    };

    private static ShiyuDefenseBuff Buff(string title, string text) => new() { Title = title, Text = text };

    private static ShiyuDefenseMonster Monster(int id, string name, int weakness)
    {
        var monster = new ShiyuDefenseMonster
        {
            Id = id,
            Name = name,
            WeakElementType = 0,
            BgIcon = null,
            IconUrl = null,
            RaceIcon = null,
        };

        switch (weakness)
        {
            case 0:
                monster.IceWeakness = 1;
                break;
            case 1:
                monster.FireWeakness = 1;
                break;
            case 2:
                monster.ElecWeakness = 1;
                break;
            case 3:
                monster.EtherWeakness = 1;
                break;
            case 4:
                monster.PhysicsWeakness = 1;
                break;
            case 5:
                monster.IceWeakness = 1;
                monster.FireWeakness = -1;
                break;
        }

        return monster;
    }

    private static ShiyuDefenseNode Node(int battleTime, int startId, int weakIndex)
    {
        return new ShiyuDefenseNode
        {
            Avatars = [Avatar(startId, "S", 60, 1), Avatar(startId + 1, "S", 60, 0), Avatar(startId + 2, "A", 55, 2)],
            Buddy = Buddy(30000 + startId, "S", 60),
            ElementTypeList = [weakIndex],
            MonsterInfo = new ShiyuDefenseMonsterInfo
            {
                Level = 70,
                List = [Monster(40000 + startId, "以太异形", weakIndex), Monster(40100 + startId, "装甲猎手", weakIndex)],
            },
            BattleTime = battleTime,
        };
    }

    public static ShiyuDefenseWrapper CreateV1Wrapper()
    {
        var info = new ShiyuDefenseInfo
        {
            ScheduleId = 2,
            BeginTime = new DateTimeOffset(Now.AddDays(-14)),
            EndTime = new DateTimeOffset(Now),
            HasData = true,
            MaxRating = "S",
            MaxRatingTimes = 3,
            MaxLayer = 7,
            BattleTime47 = 512,
            RatingSTimes = 3,
            RatingATimes = 1,
            RatingBTimes = 0,
            AllFloorDetail =
            [
                new ShiyuDefenseFloorDetail
                {
                    LayerId = 7,
                    LayerIndex = 7,
                    Rating = "S",
                    ZoneName = "第7防线",
                    FloorChallengeTime = Now.AddDays(-3),
                    ChallengeTime = new DateTimeOffset(Now.AddDays(-3)),
                    Buffs = [Buff("强攻·破阵", "代理人造成的伤害提升 30%。")],
                    Node1 = Node(96, 1001, 3),
                    Node2 = Node(104, 1011, 0),
                },
            ],
        };

        return new ShiyuDefenseWrapper { HadalVer = "v1", InfoV1 = info, NickName = "LunarWard", Icon = "" };
    }

    public static ShiyuDefenseWrapper CreateV2Wrapper()
    {
        var info = new ShiyuDefenseInfoV2
        {
            ScheduleId = 1,
            BeginTime = new DateTimeOffset(Now.AddDays(-2)),
            EndTime = new DateTimeOffset(Now.AddDays(12)),
            HasData = true,
            MaxRating = "S",
            MaxRatingTimes = 6,
            V2Score = 48000,
            Brief = new ShiyuDefenseV2Brief
            {
                Score = 48000,
                MaxScore = 50000,
                RankPercent = 1234,
                Rating = "S",
                BattleTime = 512,
                ChallengeTime = Now.AddHours(-5),
            },
            FifthLayerDetail = new ShiyuDefenseV2FifthLayerDetail
            {
                LayerChallenges =
                [
                    new ShiyuDefenseV2FifthLayerChallengeInfo
                    {
                        LayerId = 1,
                        BattleTime = 210,
                        Score = 16000,
                        MaxScore = 16000,
                        Rating = "S",
                        ChallengeTime = Now.AddHours(-5),
                        Buff = Buff("增伤·强攻", "代理人造成的伤害提升 20%。"),
                        AvatarList = [Avatar(2001, "S", 60, 0), Avatar(2002, "S", 60, 1), Avatar(2003, "A", 55, 2)],
                        Buddy = Buddy(31001, "S", 60),
                    },
                    new ShiyuDefenseV2FifthLayerChallengeInfo
                    {
                        LayerId = 2,
                        BattleTime = 180,
                        Score = 14000,
                        MaxScore = 16000,
                        Rating = "S",
                        ChallengeTime = Now.AddHours(-5),
                        Buff = Buff("增伤·强攻", "代理人造成的伤害提升 20%。"),
                        AvatarList = [Avatar(2011, "S", 60, 0), Avatar(2012, "S", 60, 0), Avatar(2013, "A", 55, 1)],
                        Buddy = Buddy(31011, "S", 60),
                    },
                ],
            },
            FourthLayerDetail = new ShiyuDefenseV2FourthLayerDetail
            {
                Buff = Buff("护盾·坚守", "代理人获得的护盾提升 15%。"),
                ChallengeTime = Now.AddDays(-1),
                Rating = "S",
                LayerChallenges =
                [
                    new ShiyuDefenseV2FourthLayerChallengeInfo
                    {
                        LayerId = 1,
                        BattleTime = 122,
                        AvatarList = [Avatar(2021, "S", 60, 0), Avatar(2022, "S", 60, 0), Avatar(2023, "A", 55, 2)],
                        Buddy = Buddy(31021, "S", 60),
                    },
                ],
            },
        };

        return new ShiyuDefenseWrapper { HadalVer = "v2", InfoV2 = info, NickName = "LunarWard", Icon = "" };
    }
}
