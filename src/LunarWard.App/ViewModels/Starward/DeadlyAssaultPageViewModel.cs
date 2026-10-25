using LunarWard.App.Services;
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
using LunarWard.Core.GameRecord.ZZZ.DeadlyAssault;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 危局强袭战页 VM（Starward <c>Features/GameRecord/ZZZ/DeadlyAssaultPage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 左侧是「当期 / 上期」两个赛期（Starward 从 SQLite 读缓存列表；LunarWard 每次进页
/// 直接调 <c>deadly_assault?schedule=1/2</c>），右侧是「绝境模式」「试炼模式」两张 680 宽的卡片，
/// 每张卡片里按节点列出 Boss / 分数 / 通关时刻 / 出战角色与邦布 / 增益。
/// </para>
/// <para>
/// 素材：Boss / 角色 / 邦布 / 增益图标来自米游社 CDN，用 <see cref="Services.StarwardAssets.BitmapAsync"/>
/// 下载；排名底图（rank-bg-*.png）与稀有度图标不在 LunarWard 打包资源里，用渐变底 + S/A 字形占位。
/// </para>
/// </remarks>
public sealed partial class DeadlyAssaultPageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static readonly Regex RichTextTag = new("<[^>]+>", RegexOptions.Compiled);

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    public DeadlyAssaultPageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _ = InitializeAsync();
    }


    /// <summary>左侧赛期列表（Starward <c>ListView_DeadlyAssault</c>）。</summary>
    public ObservableCollection<DeadlyAssaultPeriodItem> AssaultList { get; } = [];

    [ObservableProperty]
    private DeadlyAssaultPeriodItem? _currentAssault;

    partial void OnCurrentAssaultChanged(DeadlyAssaultPeriodItem? value)
    {
        ApplyCurrent(value);
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _showEmpty = true;

    [ObservableProperty]
    private string? _errorText;


    public string PeriodText
    {
        get
        {
            if (CurrentAssault is null)
            {
                return "";
            }

            var info = CurrentAssault.Info;
            return string.Format(
                Strings["SpiralAbyss.PeriodRange"],
                info.StartTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                info.EndTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
        }
    }

    public bool HasHard => CurrentAssault?.Info.HasHard == true;

    public string HardScoreText => CurrentAssault?.HardTotalScore.ToString(CultureInfo.InvariantCulture) ?? "0";

    public string HardStarText => $"x{CurrentAssault?.HardTotalStar ?? 0}";

    public string HardRankText => RankPercentText(CurrentAssault?.Info.HardRankPercent ?? 0);

    public IBrush HardRankBrush => RankPercentBrush(CurrentAssault?.Info.HardRankPercent ?? 0);

    public string TrialScoreText => CurrentAssault?.Info.TotalScore.ToString(CultureInfo.InvariantCulture) ?? "0";

    public string TrialStarText => $"x{CurrentAssault?.Info.TotalStar ?? 0}";

    public string TrialRankText => RankPercentText(CurrentAssault?.Info.RankPercent ?? 0);

    public IBrush TrialRankBrush => RankPercentBrush(CurrentAssault?.Info.RankPercent ?? 0);

    /// <summary>绝境模式节点（Starward <c>HardNodes</c>）。</summary>
    public List<DeadlyAssaultNodeItem> HardNodes => CurrentAssault?.HardNodeItems ?? [];

    /// <summary>试炼模式节点（Starward <c>AllNodes</c>）。</summary>
    public List<DeadlyAssaultNodeItem> TrialNodes => CurrentAssault?.TrialNodeItems ?? [];


    partial void OnIsBusyChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();

    private bool CanRefresh() => !IsBusy;


    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        ErrorText = null;
        try
        {
            AssaultList.Clear();
            for (var schedule = 1; schedule <= 2; schedule++)
            {
                DeadlyAssaultInfo info;
                try
                {
                    info = await _client.GetDeadlyAssaultInfoAsync(_role, schedule);
                }
                catch (miHoYoApiException)
                {
                    // 上期没有记录时接口会报错，静默跳过。
                    continue;
                }

                if (info is null)
                {
                    continue;
                }

                AssaultList.Add(new DeadlyAssaultPeriodItem(info));
            }

            CurrentAssault = AssaultList.FirstOrDefault();
            if (AssaultList.Count == 0)
            {
                ShowEmpty = true;
            }
        }
        catch (HttpRequestException ex)
        {
            ErrorText = string.Format(Strings["DeadlyAssault.NetworkError"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["DeadlyAssault.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        finally
        {
            IsBusy = false;
        }
    }


    private async Task InitializeAsync()
    {
        if (ToolboxDemo.Enabled)
        {
            ApplyDemo();
            return;
        }

        await RefreshAsync();
    }


    private void ApplyCurrent(DeadlyAssaultPeriodItem? period)
    {
        if (period is null)
        {
            ShowEmpty = true;
        }
        else
        {
            ShowEmpty = !period.Info.HasData;
        }

        OnPropertyChanged(nameof(PeriodText));
        OnPropertyChanged(nameof(HasHard));
        OnPropertyChanged(nameof(HardScoreText));
        OnPropertyChanged(nameof(HardStarText));
        OnPropertyChanged(nameof(HardRankText));
        OnPropertyChanged(nameof(HardRankBrush));
        OnPropertyChanged(nameof(TrialScoreText));
        OnPropertyChanged(nameof(TrialStarText));
        OnPropertyChanged(nameof(TrialRankText));
        OnPropertyChanged(nameof(TrialRankBrush));
        OnPropertyChanged(nameof(HardNodes));
        OnPropertyChanged(nameof(TrialNodes));
        _ = LoadIconsAsync();
    }


    private async Task LoadIconsAsync()
    {
        var period = CurrentAssault;
        if (period is null)
        {
            return;
        }

        var assets = Shell.Assets;
        foreach (var node in period.TrialNodeItems.Concat(period.HardNodeItems))
        {
            if (!string.IsNullOrWhiteSpace(node.BossBgUrl))
            {
                node.BossBg = await assets.BitmapAsync(node.BossBgUrl).ConfigureAwait(true);
            }

            if (!string.IsNullOrWhiteSpace(node.BossIconUrl))
            {
                node.BossIcon = await assets.BitmapAsync(node.BossIconUrl).ConfigureAwait(true);
            }

            if (!string.IsNullOrWhiteSpace(node.BossRaceIconUrl))
            {
                node.BossRaceIcon = await assets.BitmapAsync(node.BossRaceIconUrl).ConfigureAwait(true);
            }

            if (!string.IsNullOrWhiteSpace(node.BuffIconUrl))
            {
                node.BuffIcon = await assets.BitmapAsync(node.BuffIconUrl).ConfigureAwait(true);
            }

            foreach (var avatar in node.Avatars)
            {
                if (!string.IsNullOrWhiteSpace(avatar.Url))
                {
                    avatar.Icon = await assets.BitmapAsync(avatar.Url).ConfigureAwait(true);
                }
            }

            if (node.Buddy is { } buddy && !string.IsNullOrWhiteSpace(buddy.Url))
            {
                buddy.Icon = await assets.BitmapAsync(buddy.Url).ConfigureAwait(true);
            }
        }
    }


    /// <summary>Starward <c>RankPercentText</c>：value 以 0.01% 为单位。</summary>
    public static string RankPercentText(int value)
    {
        var d = value / 100;
        var p = value % 100;
        return $"{d}.{p:D2}%";
    }

    /// <summary>
    /// Starward <c>RankBackground</c> 的 5 档排名底图（rank-bg-1..5.png）不在仓库里，
    /// 用同档位的渐变矩形 + 黑色粗体百分比占位。
    /// </summary>
    public static IBrush RankPercentBrush(int value) => value switch
    {
        <= 100 => Gradient("#FFE07A", "#FF8A00"),
        <= 200 => Gradient("#FFC2D1", "#FF5C8A"),
        <= 500 => Gradient("#DCC7FF", "#8A5CFF"),
        <= 3000 => Gradient("#BFE4FF", "#3D8BFF"),
        _ => Gradient("#D7DDE4", "#8792A0"),
    };

    private static IBrush Gradient(string top, string bottom) => new LinearGradientBrush
    {
        StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
        EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
        GradientStops =
        {
            new Avalonia.Media.GradientStop(Color.Parse(top), 0),
            new Avalonia.Media.GradientStop(Color.Parse(bottom), 1),
        },
    };


    private void ApplyDemo()
    {
        ErrorText = null;
        AssaultList.Clear();

        var currentStart = new DateTime(2026, 10, 10);
        var currentEnd = new DateTime(2026, 10, 24);
        AssaultList.Add(new DeadlyAssaultPeriodItem(BuildDemoInfo(currentStart, currentEnd, 1, 1234)));
        AssaultList.Add(new DeadlyAssaultPeriodItem(BuildDemoInfo(currentStart.AddDays(-14), currentStart, 2, 5000)));
        CurrentAssault = AssaultList.FirstOrDefault();
    }


    private static DeadlyAssaultInfo BuildDemoInfo(DateTime start, DateTime end, int zoneId, int rankPercent)
    {
        var avatars = new List<ZZZAvatar>
        {
            new() { Id = 1011, Level = 60, Rarity = "S", Rank = 1 },
            new() { Id = 1021, Level = 60, Rarity = "S", Rank = 0 },
            new() { Id = 1031, Level = 55, Rarity = "A", Rank = 2 },
        };
        var buddy = new ZZZBuddy { Id = 2001, Level = 60, Rarity = "S" };

        DeadlyAssaultNode BuildNode(int score, int star, string bossName)
        {
            return new DeadlyAssaultNode
            {
                Score = score,
                Star = star,
                TotalStar = 3,
                ChallengeTime = start.AddDays(2),
                Boss =
                [
                    new DeadlyAssaultBoss { Name = bossName },
                ],
                Buff =
                [
                    new DeadlyAssaultBuff { Name = "增伤·强攻", Desc = "<color=#FFB13F>代理人</color>造成的伤害提升 20%。" },
                ],
                Avatars = [.. avatars],
                Buddy = buddy,
            };
        }

        var trialNodes = new List<DeadlyAssaultNode>
        {
            BuildNode(18000, 2, "未知复合体"),
            BuildNode(16000, 2, "以太异形"),
            BuildNode(14000, 2, "装甲猎手"),
        };

        var hardNodes = new List<DeadlyAssaultNode>
        {
            BuildNode(65000, 1, "绝境·未知复合体"),
        };

        var info = new DeadlyAssaultInfo
        {
            Uid = 100000001,
            ZoneId = zoneId,
            StartTime = start,
            EndTime = end,
            RankPercent = rankPercent,
            AllNodes = trialNodes,
            HasData = true,
            TotalScore = trialNodes.Sum(x => x.Score),
            TotalStar = trialNodes.Sum(x => x.Star),
            HasHard = true,
            HardNodes = hardNodes,
            HardRankPercent = 321,
        };
        info.HardTotalScore = hardNodes.Sum(x => x.Score);
        info.HardTotalStar = hardNodes.Sum(x => x.Star);
        return info;
    }


    /// <summary>把 Starward <c>ColorfulTextBlock</c> 的富文本 desc 降级成纯文本。</summary>
    public static string CleanRichText(string? value)
        => string.IsNullOrWhiteSpace(value) ? "" : RichTextTag.Replace(value, "").Trim();
}


/// <summary>左侧一个赛期（Starward <c>ListView_DeadlyAssault</c> 的 <c>DeadlyAssaultInfo</c>）。</summary>
public sealed partial class DeadlyAssaultPeriodItem : ObservableObject
{
    public DeadlyAssaultPeriodItem(DeadlyAssaultInfo info)
    {
        Info = info;
        HardTotalScore = info.HardNodes?.Sum(x => x.Score) ?? 0;
        HardTotalStar = info.HardNodes?.Sum(x => x.Star) ?? 0;
        HardNodeItems = info.HardNodes?.Select(x => new DeadlyAssaultNodeItem(x, hard: true)).ToList() ?? [];
        TrialNodeItems = info.AllNodes?.Select(x => new DeadlyAssaultNodeItem(x)).ToList() ?? [];
    }

    public DeadlyAssaultInfo Info { get; }

    public int HardTotalScore { get; }

    public int HardTotalStar { get; }

    public List<DeadlyAssaultNodeItem> HardNodeItems { get; }

    public List<DeadlyAssaultNodeItem> TrialNodeItems { get; }

    public string DateText => Info.StartTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string HardStarText => $"x{HardTotalStar}";

    public string TrialStarText => $"x{Info.TotalStar}";
}


/// <summary>一个 Boss 节点（Starward <c>DeadlyAssaultNode</c>）。</summary>
public sealed partial class DeadlyAssaultNodeItem : ObservableObject
{
    public DeadlyAssaultNodeItem(DeadlyAssaultNode node, bool hard = false)
    {
        Node = node;
        StarBrush = hard
            ? new SolidColorBrush(Color.Parse("#FFA94D"))
            : new SolidColorBrush(Color.Parse("#CFE3FF"));
        var strings = Localization.AppStrings.Instance;
        var boss = node.Boss?.FirstOrDefault();
        BossName = boss?.Name ?? "";
        BuffName = node.Buff?.FirstOrDefault()?.Name ?? "";
        BuffDesc = DeadlyAssaultPageViewModel.CleanRichText(node.Buff?.FirstOrDefault()?.Desc);
        Avatars = node.Avatars?.Select(x => new DeadlyAssaultAvatarItem(x)).ToList() ?? [];
        Buddy = node.Buddy is null ? null : new DeadlyAssaultBuddyItem(node.Buddy);
        Stars = [.. Enumerable.Range(0, Math.Max(node.Star, 0))];
        ClearedText = string.Format(
            strings["DeadlyAssault.ClearedAt"],
            node.ChallengeTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
    }

    public DeadlyAssaultNode Node { get; }

    public string BossName { get; }

    public string? BossBgUrl => Node.Boss?.FirstOrDefault()?.BgIcon;

    public string? BossIconUrl => Node.Boss?.FirstOrDefault()?.Icon;

    public string? BossRaceIconUrl => Node.Boss?.FirstOrDefault()?.RaceIcon;

    public string? BuffIconUrl => Node.Buff?.FirstOrDefault()?.Icon;

    public string ScoreText => Node.Score.ToString(CultureInfo.InvariantCulture);

    public IBrush StarBrush { get; }

    public List<int> Stars { get; }

    public string ClearedText { get; }

    public string BuffName { get; }

    public string BuffDesc { get; }

    public List<DeadlyAssaultAvatarItem> Avatars { get; }

    public DeadlyAssaultBuddyItem? Buddy { get; }

    [ObservableProperty]
    private Bitmap? _bossBg;

    [ObservableProperty]
    private Bitmap? _bossIcon;

    [ObservableProperty]
    private Bitmap? _bossRaceIcon;

    [ObservableProperty]
    private Bitmap? _buffIcon;
}


/// <summary>出战角色卡（Starward 的 ZZZAvatar + 76x94 卡面）。</summary>
public sealed partial class DeadlyAssaultAvatarItem : ObservableObject
{
    public DeadlyAssaultAvatarItem(ZZZAvatar avatar)
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


/// <summary>出战邦布（Starward 的 58x78 卡面）。</summary>
public sealed partial class DeadlyAssaultBuddyItem : ObservableObject
{
    public DeadlyAssaultBuddyItem(ZZZBuddy buddy)
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
