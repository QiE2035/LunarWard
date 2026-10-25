using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.StarRail.ChallengePeak;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 异象仲裁页 VM（Starward <c>Features/GameRecord/StarRail/ChallengePeakPage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 结构 = 左栏「刷新 + 各期列表（版本号 / 王棋星 / 骑士星 / BOSS 立绘）」
/// + 右侧「统计周期 → 统计卡（主题图 + 两类星数 + 名称与挑战次数）→ 王棋通关战绩卡 → 骑士关卡战绩卡」。
/// </para>
/// <para>
/// 与其它战绩页不同：接口一次返回多期（<c>ChallengePeakRecords</c>，按 <c>GroupId</c> 分组），
/// 所以列表直接取 <c>data.ChallengePeakRecords</c>，不再按 schedule_type 逐页拉取。
/// </para>
/// </remarks>
public sealed partial class ChallengePeakPageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    private ChallengePeakData? _data;

    private bool _initialized;


    public ChallengePeakPageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _ = InitializeAsync();
    }


    /// <summary>左侧列表（Starward <c>ListView_ForgottenHall</c>：每期一张）。</summary>
    public ObservableCollection<ChallengePeakPeriodItem> PeriodList { get; } = [];

    [ObservableProperty]
    private ChallengePeakPeriodItem? _currentPeriod;

    partial void OnCurrentPeriodChanged(ChallengePeakPeriodItem? value) => ApplyCurrent(value);

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorText;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public bool HasData => CurrentPeriod is not null;

    /// <summary>统计周期（Starward <c>CurrentChallengePeakRecord.Group</c>）。</summary>
    public string PeriodText => CurrentPeriod?.Record.Group is { } group
        ? string.Format(
            Strings["SpiralAbyss.PeriodRange"],
            group.BeginTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
            group.EndTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture))
        : "";

    public string GroupName => CurrentPeriod?.Record.Group.NameMi18n ?? "";

    public string BossStarsText => CurrentPeriod?.Record.BossStars.ToString(CultureInfo.InvariantCulture) ?? "";

    public string MobStarsText => CurrentPeriod?.Record.MobStars.ToString(CultureInfo.InvariantCulture) ?? "";

    public string ChallengeAttemptText => _data?.ChallengePeakBestRecordBrief?.TotalBattleNum.ToString(CultureInfo.InvariantCulture) ?? "";

    /// <summary>王棋记录（Starward <c>CurrentChallengePeakRecord.BossRecord</c>，可能为空表示未通关）。</summary>
    public bool HasBossRecord => CurrentPeriod?.Record.BossRecord is { HasChallengeRecord: true };

    public string BossName => CurrentPeriod?.Record.BossInfo.NameMi18n ?? "";

    public string BossHardModeName => CurrentPeriod?.Record.BossInfo.HardModeNameMi18n ?? "";

    public bool IsHardMode => CurrentPeriod?.Record.BossRecord?.HardMode ?? false;

    public string BossRoundNumText => CurrentPeriod?.Record.BossRecord?.RoundNum.ToString(CultureInfo.InvariantCulture) ?? "";

    public string BossStarsGlyph => Stars(CurrentPeriod?.Record.BossStars ?? 0);

    public string BossChallengeTimeText => FormatTime(CurrentPeriod?.Record.BossRecord?.ChallengeTime);

    /// <summary>王棋 Buff（图标 + 名称 + 描述）。</summary>
    public string BossBuffName => CurrentPeriod?.Record.BossRecord?.Buff?.NameMi18n ?? "";

    public string BossBuffDesc => CurrentPeriod?.Record.BossRecord?.Buff?.DescMi18n ?? "";

    [ObservableProperty]
    private Bitmap? _bossImage;

    [ObservableProperty]
    private Bitmap? _themeImage;

    [ObservableProperty]
    private Bitmap? _bossBuffIcon;

    public ObservableCollection<ChallengePeakAvatarItem> BossAvatars { get; } = [];

    /// <summary>骑士关卡列表（Starward <c>CurrentChallengePeakRecord.Mobs</c>）。</summary>
    public ObservableCollection<ChallengePeakMobItem> Mobs { get; } = [];


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

        if (Environment.GetEnvironmentVariable("LUNARWARD_TOOLBOX_DEMO") == "1")
        {
            ApplyDemo();
            return;
        }

        IsLoading = true;
        ErrorText = null;
        try
        {
            var selected = CurrentPeriod?.Record.Group.GroupId;
            var data = await _client.GetStarRailChallengePeakDataAsync(_role, 1).ConfigureAwait(true);
            _data = data;

            PeriodList.Clear();
            foreach (var record in data.ChallengePeakRecords ?? [])
            {
                PeriodList.Add(new ChallengePeakPeriodItem(record, data));
            }

            ShowEmpty = PeriodList.Count == 0;
            if (ShowEmpty)
            {
                CurrentPeriod = null;
                return;
            }

            CurrentPeriod = PeriodList.FirstOrDefault(x => x.Record.Group.GroupId == selected) ?? PeriodList[0];
        }
        catch (miHoYoApiException ex)
        {
            ErrorText = string.Format(Strings["ChallengePeak.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorText = string.Format(Strings["ChallengePeak.NetworkError"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["ChallengePeak.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }


    private void ApplyCurrent(ChallengePeakPeriodItem? item)
    {
        BossAvatars.Clear();
        Mobs.Clear();
        if (item is not null)
        {
            if (item.Record.BossRecord?.Avatars is { Count: > 0 } avatars)
            {
                foreach (var avatar in avatars)
                {
                    BossAvatars.Add(new ChallengePeakAvatarItem(avatar));
                }
            }

            foreach (var mob in item.Record.Mobs)
            {
                Mobs.Add(new ChallengePeakMobItem(mob));
            }

            _ = LoadImagesAsync(item.Record);
        }

        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(PeriodText));
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(BossStarsText));
        OnPropertyChanged(nameof(MobStarsText));
        OnPropertyChanged(nameof(ChallengeAttemptText));
        OnPropertyChanged(nameof(HasBossRecord));
        OnPropertyChanged(nameof(BossName));
        OnPropertyChanged(nameof(BossHardModeName));
        OnPropertyChanged(nameof(IsHardMode));
        OnPropertyChanged(nameof(BossRoundNumText));
        OnPropertyChanged(nameof(BossStarsGlyph));
        OnPropertyChanged(nameof(BossChallengeTimeText));
        OnPropertyChanged(nameof(BossBuffName));
        OnPropertyChanged(nameof(BossBuffDesc));
    }


    /// <summary>下载 BOSS 立绘、主题图、Buff 图标与头像（失败保留占位）。</summary>
    private async Task LoadImagesAsync(ChallengePeakRecord record)
    {
        var assets = Shell.Assets;
        BossImage = await assets.BitmapAsync(record.BossInfo?.Icon).ConfigureAwait(true);
        ThemeImage = await assets.BitmapAsync(record.Group?.ThemePicPath).ConfigureAwait(true);
        BossBuffIcon = await assets.BitmapAsync(record.BossRecord?.Buff?.Icon).ConfigureAwait(true);

        // 左侧列表卡片右上角的 BOSS 立绘（源码同一张 BossIcon）。
        if (CurrentPeriod is { } period)
        {
            period.BossIcon ??= BossImage;
            OnPropertyChanged(nameof(CurrentPeriod));
        }

        foreach (var avatar in BossAvatars)
        {
            if (!string.IsNullOrWhiteSpace(avatar.Url))
            {
                avatar.Icon = await assets.BitmapAsync(avatar.Url).ConfigureAwait(true);
            }
        }

        foreach (var mob in Mobs)
        {
            mob.MonsterIcon = await assets.BitmapAsync(mob.Mob.MobInfo.MonsterIcon).ConfigureAwait(true);
        }
    }


    private static string Stars(int count) => count <= 0 ? "" : new string('★', Math.Clamp(count, 0, 5));

    private static string FormatTime(DateTime? time)
        => time?.ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture) ?? "";


    /// <summary>
    /// 自检/截图夹具（<c>LUNARWARD_TOOLBOX_DEMO=1</c>）：不联网，灌两期演示数据
    /// （首期王棋已通关并带 Buff，次期未通关，骑士关卡含快速通关）。
    /// </summary>
    private void ApplyDemo()
    {
        var now = DateTimeOffset.Now;
        var group1 = new ChallengePeakGroup
        {
            GroupId = 301,
            GameVersion = "4.6",
            NameMi18n = "异象仲裁·静止的审判",
            BeginTime = now.AddDays(-3).DateTime,
            EndTime = now.AddDays(11).DateTime,
            ThemePicPath = "",
        };
        var group2 = new ChallengePeakGroup
        {
            GroupId = 300,
            GameVersion = "4.5",
            NameMi18n = "异象仲裁·被遗忘的战场",
            BeginTime = now.AddDays(-17).DateTime,
            EndTime = now.AddDays(-3).DateTime,
            ThemePicPath = "",
        };

        var record1 = BuildDemoRecord(group1, cleared: true);
        var record2 = BuildDemoRecord(group2, cleared: false);

        _data = new ChallengePeakData
        {
            Uid = _role.Uid,
            GroupId = group1.GroupId,
            GameVersion = group1.GameVersion,
            BossStars = group1.GroupId == 301 ? 3 : 1,
            MobStars = group1.GroupId == 301 ? 8 : 5,
            ChallengePeakBestRecordBrief = new ChallengePeakBestRecordBrief
            {
                TotalBattleNum = 6,
                BossStars = 3,
                MobStars = 8,
            },
            ChallengePeakRecords = [record1, record2],
        };

        PeriodList.Clear();
        PeriodList.Add(new ChallengePeakPeriodItem(record1, _data));
        PeriodList.Add(new ChallengePeakPeriodItem(record2, _data));
        ShowEmpty = false;
        CurrentPeriod = PeriodList[0];
    }


    private ChallengePeakRecord BuildDemoRecord(ChallengePeakGroup group, bool cleared)
    {
        var now = DateTimeOffset.Now;
        var bossRecord = cleared
            ? new ChallengePeakBossRecord
            {
                MazeId = 9001,
                HasChallengeRecord = true,
                ChallengeTime = now.AddDays(-1).UtcDateTime,
                HardMode = true,
                RoundNum = 4,
                StarNum = 3,
                Avatars = BuildDemoAvatars(0, 4),
                Buff = new ChallengePeakBuff
                {
                    Id = 1,
                    NameMi18n = "智识的恩赐",
                    DescMi18n = "施放终结技后，使我方全体获得 1 层「智识」，每层提升 8% 暴击伤害。",
                    Icon = "",
                },
            }
            : new ChallengePeakBossRecord { MazeId = 9001, HasChallengeRecord = false };

        var mobInfos = new List<ChallengePeakMobInfo>
        {
            new() { MazeId = 8001, Name = "第一位骑士", MonsterName = "· 灰熊", MonsterIcon = "" },
            new() { MazeId = 8002, Name = "第二位骑士", MonsterName = "· 迷失者", MonsterIcon = "" },
        };
        var mobRecords = new List<ChallengePeakMobRecord>();
        for (var i = 0; i < 2; i++)
        {
            mobRecords.Add(new ChallengePeakMobRecord
            {
                MazeId = 8001 + i,
                HasChallengeRecord = cleared,
                ChallengeTime = now.AddDays(-2).UtcDateTime,
                RoundNum = 2,
                StarNum = i == 0 ? 3 : 2,
                // 第二位骑士演示「快速通关」：源码同样隐藏队伍只留 20004.png。
                IsFast = i == 1,
                Avatars = i == 1 ? [] : BuildDemoAvatars(i, 4),
            });
        }

        return new ChallengePeakRecord
        {
            Group = group,
            BossInfo = new ChallengePeakBossInfo
            {
                MazeId = 9001,
                NameMi18n = "记忆的店主",
                HardModeNameMi18n = "硬核模式",
                Icon = "",
            },
            MobInfos = mobInfos,
            MobRecords = mobRecords,
            HasChallengeRecord = cleared,
            BattleNum = 6,
            BossStars = cleared ? 3 : 1,
            MobStars = cleared ? 8 : 5,
            BossRecord = bossRecord,
        };
    }


    private static List<ChallengePeakAvatar> BuildDemoAvatars(int index, int count)
    {
        var avatars = new List<ChallengePeakAvatar>();
        for (var i = 0; i < count; i++)
        {
            avatars.Add(new ChallengePeakAvatar
            {
                Id = 1200 + index * 10 + i,
                Level = 80,
                Rarity = i % 2 == 0 ? 5 : 4,
                Rank = i == 0 ? 1 : 0,
                Element = "Quantum",
                Icon = "",
            });
        }

        return avatars;
    }
}


/// <summary>左侧列表的一期（Starward 的 <c>ChallengePeakData</c> 行：版本号 + 两类星数 + BOSS 头像）。</summary>
public sealed class ChallengePeakPeriodItem
{
    public ChallengePeakPeriodItem(ChallengePeakRecord record, ChallengePeakData? data)
    {
        Record = record;
        Data = data;
    }

    public ChallengePeakRecord Record { get; }

    public ChallengePeakData? Data { get; }

    public string GameVersion => string.IsNullOrWhiteSpace(Record.Group?.GameVersion)
        ? Record.Group?.NameMi18n ?? ""
        : Record.Group.GameVersion;

    public string BossStars => Record.BossStars.ToString(CultureInfo.InvariantCulture);

    public string MobStars => Record.MobStars.ToString(CultureInfo.InvariantCulture);

    /// <summary>第 1 张卡片的布局用 112 宽，右侧需要 BOSS 立绘（远程图，空的就只留白）。</summary>
    public Bitmap? BossIcon { get; set; }
}


/// <summary>出战角色卡（源码 <c>StarRailTeamDataTemplate</c>：72×84 + 稀有度底 + 星魂 + Lv）。</summary>
public sealed partial class ChallengePeakAvatarItem : ObservableObject
{
    public ChallengePeakAvatarItem(ChallengePeakAvatar avatar)
    {
        Url = avatar.Icon;
        LevelText = $"Lv.{avatar.Level}";
        BackgroundBrush = avatar.Rarity >= 5
            ? new SolidColorBrush(Color.Parse("#FFC29B62"))
            : new SolidColorBrush(Color.Parse("#FFA15EBC"));
        HasRank = avatar.Rank > 0;
        RankText = avatar.Rank.ToString(CultureInfo.InvariantCulture);
    }

    public string? Url { get; }

    public string LevelText { get; }

    public IBrush BackgroundBrush { get; }

    public bool HasRank { get; }

    public string RankText { get; }

    [ObservableProperty]
    private Bitmap? _icon;
}


/// <summary>骑士关卡（源码 <c>ChallengePeakRecordMob</c>：信息 + 可选战绩）。</summary>
public sealed partial class ChallengePeakMobItem : ObservableObject
{
    public ChallengePeakMobItem(ChallengePeakRecordMob mob)
    {
        Mob = mob;
        NameText = $"{mob.MobInfo.Name}{mob.MobInfo.MonsterName}";
        StarGlyph = new string('★', Math.Clamp(mob.MobRecord?.StarNum ?? 0, 0, 5));
        Avatars = new((mob.MobRecord?.Avatars ?? []).Select(x => new ChallengePeakAvatarItem(x)));
    }

    public ChallengePeakRecordMob Mob { get; }

    public string NameText { get; }

    public bool HasChallengeRecord => Mob.HasChallengeRecord;

    public string RoundNumText => Mob.MobRecord?.RoundNum.ToString(CultureInfo.InvariantCulture) ?? "";

    public string StarGlyph { get; }

    public bool IsFast => Mob.MobRecord?.IsFast ?? false;

    public bool ShowTeam => HasChallengeRecord && !IsFast;

    public string ChallengeTimeText => Mob.MobRecord?.ChallengeTime.ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture) ?? "";

    [ObservableProperty]
    private Bitmap? _monsterIcon;

    public ObservableCollection<ChallengePeakAvatarItem> Avatars { get; }

    /// <summary>XAML 文案索引器（骑士卡在 ItemsControl 里）。</summary>
    public Localization.AppStrings Strings => Localization.AppStrings.Instance;
}
