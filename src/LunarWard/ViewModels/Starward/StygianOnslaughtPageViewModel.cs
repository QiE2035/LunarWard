using LunarWard.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.Genshin.StygianOnslaught;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 幽境危战页 VM（Starward <c>Features/GameRecord/Genshin/StygianOnslaughtPage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 左侧是各期（日期 + 难度勋章 + 用时 seconds），右侧是「统计周期 → 最佳记录 +
/// 单人/多人切换 → 每个 Boss 的卡片（阵容 / 怪物立绘 / 怪物等级 / 描述 / 最强一击）」。
/// </para>
/// <para>
/// Starward 走 SQLite 缓存 + 手动刷新；LunarWard 进页直接调
/// <c>GetStygianOnslaughtInfosAsync</c>（一次返回全部期的完整数据），刷新按钮重拉。
/// 难度勋章、元素/头像图走 <see cref="Services.StarwardAssets.BitmapAsync"/> 或
/// 缺省占位（无本地 PNG 素材，见 XAML 注释）。
/// </para>
/// </remarks>
public sealed partial class StygianOnslaughtPageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    private bool _initialized;


    public StygianOnslaughtPageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _ = InitializeAsync();
    }


    /// <summary>左侧周期列表（Starward <c>ListView_StygianOnslaughtPageList</c>）。</summary>
    public ObservableCollection<StygianPeriodItem> PeriodList { get; } = [];

    [ObservableProperty]
    private StygianPeriodItem? _currentPeriod;

    partial void OnCurrentPeriodChanged(StygianPeriodItem? value) => ApplyCurrent(value);

    [ObservableProperty]
    private StygianOnslaughtInfo? _currentInfo;

    [ObservableProperty]
    private StygianOnslaughtBattle? _currentBattle;

    /// <summary>true = 单人（Starward <c>Segmented_PlayerMode.SelectedIndex == 0</c>）。</summary>
    [ObservableProperty]
    private bool _isSinglePlayer = true;

    /// <summary>多人模式是否有数据（Starward 用它 IsEnabled 第二个 SegmentedItem）。</summary>
    [ObservableProperty]
    private bool _canSelectMultiPlayer;

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorText;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public bool HasData => CurrentInfo is not null;

    public string PeriodText => CurrentInfo?.Schedule is { } schedule
        ? string.Format(
            Strings["SpiralAbyss.PeriodRange"],
            schedule.StartDateTime.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture),
            schedule.EndDateTime.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture))
        : "";

    /// <summary>「最佳记录」右侧的难度（0 = 无记录；XAML 用数字圆章占位，Starward 是勋章 PNG）。</summary>
    public int BestDifficulty => CurrentBattle?.Best?.Difficulty ?? 0;

    public string BestSecondsText => CurrentBattle?.Best is { Seconds: > 0 } best
        ? $"{best.Seconds}s"
        : "—";

    public string BestDifficultyColor => BestDifficulty switch
    {
        >= 6 => "#C8A2FF",
        >= 4 => "#E4C891",
        _ => "#B0B0B0",
    };

    /// <summary>每个 Boss 一张卡（Starward <c>CurrentSelectedBattle.Challenge</c>）。</summary>
    public ObservableCollection<StygianChallengeItem> Challenges { get; } = [];


    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_initialized)
        {
            await InitializeAsync();
        }
    }


    [RelayCommand]
    private void SelectSinglePlayer()
    {
        IsSinglePlayer = true;
        ApplyBattle(CurrentInfo?.SinglePlayer);
    }


    [RelayCommand]
    private void SelectMultiPlayer()
    {
        if (!CanSelectMultiPlayer)
        {
            return;
        }

        IsSinglePlayer = false;
        ApplyBattle(CurrentInfo?.MultiPlayer);
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
            var infos = await _client.GetStygianOnslaughtInfosAsync(_role).ConfigureAwait(true);

            var selected = CurrentPeriod?.Info.ScheduleId;
            PeriodList.Clear();
            foreach (var info in infos
                         .OrderByDescending(x => x.StartDateTime)
                         .ThenByDescending(x => x.ScheduleId))
            {
                PeriodList.Add(new StygianPeriodItem(info));
            }

            ShowEmpty = PeriodList.Count == 0;
            if (ShowEmpty)
            {
                CurrentPeriod = null;
                CurrentInfo = null;
                ApplyBattle(null);
                return;
            }

            CurrentPeriod = PeriodList.FirstOrDefault(x => x.Info.ScheduleId == selected) ?? PeriodList[0];
        }
        catch (miHoYoApiException ex)
        {
            ErrorText = string.Format(Strings["Stygian.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorText = string.Format(Strings["Stygian.NetworkError"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["Stygian.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }


    private void ApplyCurrent(StygianPeriodItem? item)
    {
        CurrentInfo = item?.Info;
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(PeriodText));
        IsSinglePlayer = true;
        CanSelectMultiPlayer = CurrentInfo?.MultiPlayer?.HasData ?? false;
        ApplyBattle(CurrentInfo?.SinglePlayer);
    }


    private void ApplyBattle(StygianOnslaughtBattle? battle)
    {
        CurrentBattle = battle;
        Challenges.Clear();
        if (battle?.Challenge is { Count: > 0 } challenges)
        {
            foreach (var challenge in challenges)
            {
                Challenges.Add(new StygianChallengeItem(challenge));
            }
        }

        OnPropertyChanged(nameof(BestDifficulty));
        OnPropertyChanged(nameof(BestDifficultyColor));
        OnPropertyChanged(nameof(BestSecondsText));
        _ = LoadIconsAsync();
    }


    /// <summary>下载阵容头像 / 怪物立绘 / 最强一击的元素图标（失败不影响文字）。</summary>
    private async Task LoadIconsAsync()
    {
        var assets = Shell.Assets;
        var targets = new List<(string? Url, Action<Bitmap> Apply)>();
        foreach (var challenge in Challenges)
        {
            targets.Add((challenge.MonsterIconUrl, b => challenge.MonsterImage = b));
            foreach (var avatar in challenge.Teams)
            {
                targets.Add((avatar.Url, b => avatar.Icon = b));
            }

            foreach (var best in challenge.BestAvatars)
            {
                targets.Add((best.Url, b => best.SideIcon = b));
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


    /// <summary>
    /// 自检/截图夹具（<c>LUNARWARD_TOOLBOX_DEMO=1</c>）：不联网，给页面灌两期演示数据。
    /// 与 Starward 的 x:DesignInstance 同用途；正常启动不会走到这里。
    /// </summary>
    private void ApplyDemo()
    {
        var now = DateTimeOffset.Now;
        var periods = new List<StygianOnslaughtInfo>();
        for (var i = 0; i < 2; i++)
        {
            var start = new DateTimeOffset(now.Year, now.Month, now.Day, 10, 0, 0, now.Offset).AddDays(-14 * i);
            var info = new StygianOnslaughtInfo
            {
                ScheduleId = 100 + i,
                Uid = _role.Uid,
                StartDateTime = start.DateTime,
                EndDateTime = start.AddDays(14).DateTime,
                Difficulty = 5 - i,
                Second = 200 + i * 10,
                Schedule = new StygianOnslaughtSchedule
                {
                    ScheduleId = 100 + i,
                    StartDateTime = start.DateTime,
                    EndDateTime = start.AddDays(14).DateTime,
                },
                SinglePlayer = BuildBattle(5 - i, 200 + i * 10),
                MultiPlayer = new StygianOnslaughtBattle { HasData = false },
            };
            periods.Add(info);
        }

        PeriodList.Clear();
        foreach (var info in periods)
        {
            PeriodList.Add(new StygianPeriodItem(info));
        }

        ShowEmpty = false;
        CurrentPeriod = PeriodList[0];
    }


    private static StygianOnslaughtBattle BuildBattle(int difficulty, int seconds)
    {
        var battle = new StygianOnslaughtBattle
        {
            Best = new StygianOnslaughtBest { Difficulty = difficulty, Seconds = seconds },
            HasData = true,
            Challenge = [],
        };

        var names = new[] { "深邃摹结株·异种", "荒野幽影", "熔岩游像" };
        var levels = new[] { 95, 90, 85 };
        for (var i = 0; i < names.Length; i++)
        {
            var monster = new StygianOnslaughtMonster
            {
                Name = names[i],
                Level = levels[i],
                Desc = [$"演示怪物 {i + 1}：无本地描述数据。", "进入范围后会持续攻击。"],
            };
            var teams = new List<StygianOnslaughtAvatar>();
            for (var k = 0; k < 4; k++)
            {
                teams.Add(new StygianOnslaughtAvatar
                {
                    Name = $"角色{k + 1}",
                    Level = 90,
                    Rarity = k % 2 == 0 ? 5 : 4,
                    Rank = k == 0 ? 1 : 0,
                });
            }

            var bestAvatars = new List<StygianOnslaughtBestAvatar>
            {
                new() { Type = 1, DPS = 123456 },
                new() { Type = 2, DPS = 987654 },
            };

            battle.Challenge.Add(new StygianOnslaughtChallenge
            {
                Name = names[i],
                Second = seconds - i * 12,
                Monster = monster,
                Teams = teams,
                BestAvatar = bestAvatars,
            });
        }

        return battle;
    }
}


/// <summary>左侧一期（Starward <c>StygianOnslaughtInfo</c> 的一行）。</summary>
public sealed class StygianPeriodItem(StygianOnslaughtInfo info)
{
    public StygianOnslaughtInfo Info { get; } = info;

    public string DateText => Info.StartDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public int Difficulty => Info.Difficulty;

    /// <summary>XAML 里难度圆章的颜色档位（Starward 用勋章 PNG，见页面 XAML 注释）。</summary>
    public string DifficultyColor => Difficulty switch
    {
        >= 6 => "#C8A2FF",
        >= 4 => "#E4C891",
        _ => "#B0B0B0",
    };

    public string SecondText => $"{Info.Second}s";
}


/// <summary>一个 Boss 卡片（Starward <c>StygianOnslaughtChallenge</c>）。</summary>
public sealed partial class StygianChallengeItem : ObservableObject
{
    public StygianChallengeItem(StygianOnslaughtChallenge challenge)
    {
        Name = challenge.Name ?? "";
        SecondsText = $"{challenge.Second}s";
        MonsterName = challenge.Monster?.Name ?? "";
        MonsterLevelText = challenge.Monster is { } monster ? $"Lv.{monster.Level}" : "";
        MonsterDesc = challenge.Monster?.TrimmedDesc ?? [];
        MonsterIconUrl = challenge.Monster?.Icon;
        Teams = challenge.Teams?.Select(x => new StygianAvatarItem(x)).ToList() ?? [];
        BestAvatars = challenge.BestAvatar?.Select(x => new StygianBestAvatarItem(x)).ToList() ?? [];
    }

    public string Name { get; }

    public string SecondsText { get; }

    public string MonsterName { get; }

    public string MonsterLevelText { get; }

    public string? MonsterIconUrl { get; }

    public List<string> MonsterDesc { get; }

    public Localization.AppStrings Strings { get; } = Localization.AppStrings.Instance;

    public List<StygianAvatarItem> Teams { get; }

    public List<StygianBestAvatarItem> BestAvatars { get; }

    [ObservableProperty]
    private Bitmap? _monsterImage;
}


/// <summary>阵容里的一格（Starward <c>StygianOnslaughtAvatar</c>：80×97、稀有度底、Rank 角标）。</summary>
public sealed partial class StygianAvatarItem(StygianOnslaughtAvatar avatar) : ObservableObject
{
    public string? Url { get; } = avatar.Image;

    public int Rarity { get; } = avatar.Rarity;

    public string LevelText { get; } = $"Lv.{avatar.Level}";

    public int Rank { get; } = avatar.Rank;

    public bool HasRank => Rank > 0;

    [ObservableProperty]
    private Bitmap? _icon;
}


/// <summary>「最强一击 / 最高总伤」的一格（Starward <c>StygianOnslaughtBestAvatar</c>）。</summary>
public sealed partial class StygianBestAvatarItem(StygianOnslaughtBestAvatar avatar) : ObservableObject
{
    public string? Url { get; } = avatar.SideIcon;

    public string TypeText { get; } = avatar.Type switch
    {
        1 => Localization.AppStrings.Instance["SpiralAbyss.StrongestSingleStrike"],
        2 => Localization.AppStrings.Instance["Stygian.HighestTotalDamage"],
        _ => "",
    };

    public string DpsText { get; } = avatar.DPS.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty]
    private Bitmap? _sideIcon;
}
