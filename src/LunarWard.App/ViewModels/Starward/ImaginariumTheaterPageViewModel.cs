using System.Collections.ObjectModel;
using System.Net.Http;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.Genshin.ImaginariumTheater;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 幻想真境剧诗页 VM（Starward <c>Features/GameRecord/Genshin/ImaginariumTheaterPage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 左侧是各期（日期 + 难度/最大幕数 + 星章数），右侧是「演出回顾」「战斗统计」「参演名单」
/// 三块。Starward 从 SQLite 读列表、选中后按 ScheduleId 读完整对象；LunarWard 用
/// <c>need_detail=true</c> 的 <c>GetImaginariumTheaterInfosAsync</c> 一次拿全。
/// </para>
/// <para>
/// 素材：头像 / Buff 图标来自米游社 CDN（<see cref="Services.StarwardAssets.BitmapAsync"/>）；
/// 勋章、幻剧之花、星章 PNG 不在 LunarWard 打包资源里，用颜色块 + 字形占位并注释。
/// </para>
/// </remarks>
public sealed partial class ImaginariumTheaterPageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    private bool _initialized;


    public ImaginariumTheaterPageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _ = InitializeAsync();
    }


    /// <summary>左侧各期（Starward <c>ListView_TheaterList</c>）。</summary>
    public ObservableCollection<TheaterPeriodItem> TheaterList { get; } = [];

    [ObservableProperty]
    private TheaterPeriodItem? _currentTheater;

    partial void OnCurrentTheaterChanged(TheaterPeriodItem? value) => ApplyCurrent(value);

    [ObservableProperty]
    private ImaginariumTheaterInfo? _info;

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorText;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public string PeriodText => Info?.StartTime.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture) is { } start
        && Info?.EndTime.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture) is { } end
        ? string.Format(Strings["SpiralAbyss.PeriodRange"], start, end)
        : "";

    public string DifficultyModeText => Info?.Stat?.DifficultyId switch
    {
        1 => Strings["Theater.Mode.1"],
        2 => Strings["Theater.Mode.2"],
        3 => Strings["Theater.Mode.3"],
        4 => Strings["Theater.Mode.4"],
        5 => Strings["Theater.Mode.5"],
        _ => "",
    };

    public string ActText => string.Format(Strings["Theater.Act"], Info?.Stat?.MaxRoundId ?? 0);

    public bool LunarVisible => Info?.Stat?.DifficultyId == 5;

    public string ArcanaText => $"{Strings["Theater.Arcana"]} {Info?.Stat?.TarotFinishedCnt ?? 0}";

    /// <summary>勋章色档（无勋章 PNG，页头/列表用同色系圆章占位）。</summary>
    public string HeraldryColor => Info?.Stat?.Heraldry switch
    {
        >= 4 => "#C8A2FF",
        >= 2 => "#E4C891",
        _ => "#B0B0B0",
    };

    /// <summary>「明星挑战星章」的逐幕星章（Starward <c>GetMedalRoundList</c>：1 = 已点亮）。</summary>
    public ObservableCollection<TheaterStarItem> MedalStars { get; } = [];

    public string CoinText => (Info?.Stat?.CoinNum ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string AvatarBonusText => (Info?.Stat?.AvatarBonusNum ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string RentText => (Info?.Stat?.RentCnt ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>战斗统计是否有数据（Starward <c>TotalUseTime != 0</c>）。</summary>
    public bool HasFightStats => (Info?.Detail?.FightStatisic?.TotalUseTime ?? 0) > 0;

    public string TotalDurationText => FormatPerformanceTime(Info?.Detail?.FightStatisic?.TotalUseTime ?? 0);

    public TheaterFightAvatarItem? MaxDamageAvatar { get; private set; }

    public TheaterFightAvatarItem? MaxDefeatAvatar { get; private set; }

    public TheaterFightAvatarItem? MaxTakeDamageAvatar { get; private set; }

    public bool HasMaxDamageAvatar => MaxDamageAvatar is not null;

    public bool HasMaxDefeatAvatar => MaxDefeatAvatar is not null;

    public bool HasMaxTakeDamageAvatar => MaxTakeDamageAvatar is not null;

    public ObservableCollection<TheaterFightAvatarItem> ShortestAvatars { get; } = [];

    /// <summary>参演名单（Starward <c>Detail.RoundsData</c>）。</summary>
    public ObservableCollection<TheaterRoundItem> Rounds { get; } = [];

    /// <summary>最佳记录（Starward 演出回顾第一行）。</summary>
    public string BestRecordText => $"{DifficultyModeText} {ActText}";


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
            var infos = await _client.GetImaginariumTheaterInfosAsync(_role).ConfigureAwait(true);

            var selected = CurrentTheater?.Info.ScheduleId;
            TheaterList.Clear();
            foreach (var info in infos
                         .OrderByDescending(x => x.StartTime)
                         .ThenByDescending(x => x.ScheduleId))
            {
                TheaterList.Add(new TheaterPeriodItem(info));
            }

            ShowEmpty = TheaterList.Count == 0;
            if (ShowEmpty)
            {
                CurrentTheater = null;
                return;
            }

            CurrentTheater = TheaterList.FirstOrDefault(x => x.Info.ScheduleId == selected) ?? TheaterList[0];
        }
        catch (miHoYoApiException ex)
        {
            ErrorText = string.Format(Strings["Theater.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorText = string.Format(Strings["Theater.NetworkError"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["Theater.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }


    private void ApplyCurrent(TheaterPeriodItem? item)
    {
        Info = item?.Info;
        HasData = Info?.HasData ?? false;
        ShowEmpty = !HasData;
        MedalStars.Clear();
        if (Info?.Stat?.GetMedalRoundList is { Count: > 0 } medals)
        {
            foreach (var value in medals)
            {
                MedalStars.Add(new TheaterStarItem(value != 0));
            }
        }

        MaxDamageAvatar = CreateFightAvatar(Info?.Detail?.FightStatisic?.MaxDamageAvatar);
        MaxDefeatAvatar = CreateFightAvatar(Info?.Detail?.FightStatisic?.MaxDefeatAvatar);
        MaxTakeDamageAvatar = CreateFightAvatar(Info?.Detail?.FightStatisic?.MaxTakeDamageAvatar);

        ShortestAvatars.Clear();
        if (Info?.Detail?.FightStatisic?.ShortestAvatarList is { Count: > 0 } shorts)
        {
            foreach (var avatar in shorts)
            {
                if (CreateFightAvatar(avatar) is { } shortAvatar)
                {
                    ShortestAvatars.Add(shortAvatar);
                }
            }
        }

        Rounds.Clear();
        if (Info?.Detail?.RoundsData is { Count: > 0 } rounds)
        {
            foreach (var round in rounds)
            {
                Rounds.Add(new TheaterRoundItem(round, Strings));
            }
        }

        NotifyHeaderChanged();
        _ = LoadIconsAsync();
    }


    private void NotifyHeaderChanged()
    {
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(PeriodText));
        OnPropertyChanged(nameof(DifficultyModeText));
        OnPropertyChanged(nameof(ActText));
        OnPropertyChanged(nameof(LunarVisible));
        OnPropertyChanged(nameof(ArcanaText));
        OnPropertyChanged(nameof(CoinText));
        OnPropertyChanged(nameof(AvatarBonusText));
        OnPropertyChanged(nameof(RentText));
        OnPropertyChanged(nameof(HasFightStats));
        OnPropertyChanged(nameof(TotalDurationText));
        OnPropertyChanged(nameof(BestRecordText));
        OnPropertyChanged(nameof(MaxDamageAvatar));
        OnPropertyChanged(nameof(MaxDefeatAvatar));
        OnPropertyChanged(nameof(MaxTakeDamageAvatar));
        OnPropertyChanged(nameof(HasMaxDamageAvatar));
        OnPropertyChanged(nameof(HasMaxDefeatAvatar));
        OnPropertyChanged(nameof(HasMaxTakeDamageAvatar));
        OnPropertyChanged(nameof(HeraldryColor));
    }


    private static TheaterFightAvatarItem? CreateFightAvatar(ImaginariumTheaterFightStatisicAvatar? avatar)
        => avatar is null ? null : new TheaterFightAvatarItem(avatar);


    private static string FormatPerformanceTime(int seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Minutes}m {ts.Seconds}s";
    }


    /// <summary>下载头像与 Buff 图标（失败只影响图，不影响文字）。</summary>
    private async Task LoadIconsAsync()
    {
        var assets = Shell.Assets;
        var targets = new List<(string? Url, Action<Bitmap> Apply)>();
        foreach (var avatar in new[] { MaxDamageAvatar, MaxDefeatAvatar, MaxTakeDamageAvatar })
        {
            if (avatar is not null)
            {
                targets.Add((avatar.Url, b => avatar.Icon = b));
            }
        }

        foreach (var avatar in ShortestAvatars)
        {
            targets.Add((avatar.Url, b => avatar.Icon = b));
        }

        foreach (var round in Rounds)
        {
            foreach (var avatar in round.Avatars)
            {
                targets.Add((avatar.Url, b => avatar.Icon = b));
            }

            foreach (var buff in round.SplendourBuffs)
            {
                targets.Add((buff.Url, b => buff.Icon = b));
            }

            foreach (var buff in round.WondrousBuffs)
            {
                targets.Add((buff.Url, b => buff.Icon = b));
            }

            foreach (var buff in round.ChoiceCards)
            {
                targets.Add((buff.Url, b => buff.Icon = b));
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
        var now = DateTime.Now;
        var periods = new List<ImaginariumTheaterInfo>();
        for (var i = 0; i < 2; i++)
        {
            var start = now.Date.AddDays(-14 * i);
            var info = new ImaginariumTheaterInfo
            {
                ScheduleId = 200 + i,
                Uid = _role.Uid,
                StartTime = start,
                EndTime = start.AddDays(14),
                DifficultyId = i == 0 ? 5 : 3,
                MaxRoundId = i == 0 ? 12 : 8,
                Heraldry = i == 0 ? 5 : 3,
                MedalNum = i == 0 ? 9 : 6,
                HasData = true,
                HasDetailData = true,
                Stat = new ImaginariumTheaterStat
                {
                    DifficultyId = i == 0 ? 5 : 3,
                    MaxRoundId = i == 0 ? 12 : 8,
                    Heraldry = i == 0 ? 5 : 3,
                    MedalNum = i == 0 ? 9 : 6,
                    CoinNum = 4200 - i * 1000,
                    AvatarBonusNum = 6 - i,
                    RentCnt = 3 - i,
                    TarotFinishedCnt = i == 0 ? 2 : 0,
                    GetMedalRoundList = [1, 1, 1, 1, 0, 1, 1, 0, 1, 0, 1, 0],
                },
                Detail = BuildDemoDetail(i),
            };
            periods.Add(info);
        }

        TheaterList.Clear();
        foreach (var info in periods)
        {
            TheaterList.Add(new TheaterPeriodItem(info));
        }

        ShowEmpty = false;
        CurrentTheater = TheaterList[0];
    }


    private static ImaginariumTheaterDetail BuildDemoDetail(int period)
    {
        var detail = new ImaginariumTheaterDetail
        {
            FightStatisic = new ImaginariumTheaterFightStatisic
            {
                TotalUseTime = 1285 + period * 90,
                MaxDamageAvatar = new ImaginariumTheaterFightStatisicAvatar { AvatarId = 10000002, Value = "987654" },
                MaxDefeatAvatar = new ImaginariumTheaterFightStatisicAvatar { AvatarId = 10000003, Value = "36" },
                MaxTakeDamageAvatar = new ImaginariumTheaterFightStatisicAvatar { AvatarId = 10000004, Value = "123456" },
                ShortestAvatarList =
                [
                    new ImaginariumTheaterFightStatisicAvatar { AvatarId = 10000005 },
                    new ImaginariumTheaterFightStatisicAvatar { AvatarId = 10000006 },
                    new ImaginariumTheaterFightStatisicAvatar { AvatarId = 10000007 },
                    new ImaginariumTheaterFightStatisicAvatar { AvatarId = 10000008 },
                ],
            },
            RoundsData = [],
        };

        for (var act = 1; act <= 6; act++)
        {
            var buffs = new List<ImaginariumTheaterSplendourBuffItem>();
            for (var b = 0; b < 3; b++)
            {
                buffs.Add(new ImaginariumTheaterSplendourBuffItem
                {
                    Name = $"辉彩祝福 {b + 1}",
                    Level = b == 0 ? 0 : b + 1,
                    LevelEffect =
                    [
                        new ImaginariumTheaterSplendourBuffEffect { Desc = "演示效果：角色攻击力提升。" },
                    ],
                });
            }

            detail.RoundsData.Add(new ImaginariumTheaterRoundsData
            {
                RoundId = act,
                FinishDateTime = DateTime.Now.AddDays(-act),
                IsGetMedal = act % 3 != 0,
                IsTarot = act == 6 && period == 0,
                TarotSerialNo = act == 6 ? 1 : 0,
                Avatars =
                [
                    new ImaginariumTheaterAvatar { Level = 90, Rarity = 5 },
                    new ImaginariumTheaterAvatar { Level = 90, Rarity = 5 },
                    new ImaginariumTheaterAvatar { Level = 80, Rarity = 4 },
                    new ImaginariumTheaterAvatar { Level = 80, Rarity = 4 },
                ],
                SplendourBuff = new ImaginariumTheaterSplendourBuff
                {
                    Summary = new ImaginariumTheaterSplendourBuffSummary
                    {
                        TotalLevel = 12 - act,
                        Desc = "演示：辉彩祝福汇总描述。",
                    },
                    Buffs = buffs,
                },
                Buffs =
                [
                    new ImaginariumTheaterBuff { Name = "奇妙助益 · 演示", Desc = "演示描述：提升元素伤害。" },
                ],
                ChoiceCards =
                [
                    new ImaginariumTheaterBuff { Name = "神秘收获 · 演示", Desc = "演示描述：获得额外奖励。" },
                ],
            });
        }

        return detail;
    }
}


/// <summary>左侧一期（Starward <c>ImaginariumTheaterInfo</c> 的一行）。</summary>
public sealed class TheaterPeriodItem(ImaginariumTheaterInfo info)
{
    public ImaginariumTheaterInfo Info { get; } = info;

    public string DateText => Info.StartTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public string ActOfMaxText => $"{Info.MaxRoundId}/{MaxRoundText}";

    public string MedalText => $"{Info.MedalNum}/{MaxRoundText}";

    public string MedalColor => Info.Heraldry switch
    {
        >= 4 => "#C8A2FF",
        >= 2 => "#E4C891",
        _ => "#B0B0B0",
    };

    private string MaxRoundText => Info.DifficultyId switch
    {
        1 => "3",
        2 => "6",
        3 => "8",
        4 => "10",
        5 => "12",
        _ => "-",
    };
}


/// <summary>明星挑战星章的一颗星（Starward <c>TheaterStarIconVisibilityConverter</c>）。</summary>
public sealed class TheaterStarItem(bool isGold)
{
    public bool IsGold { get; } = isGold;
}


/// <summary>战斗统计里的一格头像（Starward <c>ImaginariumTheaterFightStatisicAvatar</c>）。</summary>
public sealed partial class TheaterFightAvatarItem(ImaginariumTheaterFightStatisicAvatar avatar) : ObservableObject
{
    public string? Url { get; } = avatar.AvatarIcon;

    public string Value { get; } = avatar.Value ?? "";

    [ObservableProperty]
    private Bitmap? _icon;
}


/// <summary>一幕（Starward <c>ImaginariumTheaterRoundsData</c>）。</summary>
public sealed class TheaterRoundItem
{
    public TheaterRoundItem(ImaginariumTheaterRoundsData round, Localization.AppStrings strings)
    {
        IsGetMedal = round.IsGetMedal;
        IsTarot = round.IsTarot;
        TitleText = IsTarot
            ? string.Format(strings["Theater.ArcanaChallenge"], round.TarotSerialNo)
            : string.Format(strings["Theater.Act"], round.RoundId);
        FinishText = round.FinishDateTime.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Avatars = round.Avatars?.Select(x => new TheaterAvatarItem(x)).ToList() ?? [];
        if (round.SplendourBuff?.Summary is { TotalLevel: > 0 } summary)
        {
            HasSplendourBuff = true;
            SplendourDesc = summary.Desc ?? "";
            SplendourBuffs = round.SplendourBuff.Buffs?.Select(x => new TheaterBuffItem(x)).ToList() ?? [];
        }

        WondrousBuffs = round.Buffs?.Select(x => new TheaterBuffItem(x)).ToList() ?? [];
        ChoiceCards = round.ChoiceCards?.Select(x => new TheaterBuffItem(x)).ToList() ?? [];
    }

    public bool IsGetMedal { get; }

    public bool IsTarot { get; }

    public string TitleText { get; }

    public string FinishText { get; }

    public List<TheaterAvatarItem> Avatars { get; }

    public bool HasSplendourBuff { get; }


    public string SplendourDesc { get; }

    public List<TheaterBuffItem> SplendourBuffs { get; }

    public List<TheaterBuffItem> WondrousBuffs { get; }

    public List<TheaterBuffItem> ChoiceCards { get; }

    public string WondrousTitle => $"{Localization.AppStrings.Instance["Theater.WondrousBoon"]} ×{WondrousBuffs.Count}";

    public string ChoiceTitle => $"{Localization.AppStrings.Instance["Theater.MysteryCache"]} ×{ChoiceCards.Count}";

    public bool HasWondrousBuffs => WondrousBuffs.Count > 0;

    public bool HasChoiceCards => ChoiceCards.Count > 0;

    /// <summary>XAML 文案索引器。</summary>
    public Localization.AppStrings Strings => Localization.AppStrings.Instance;
}


/// <summary>一幕阵容里的一格（80×97、稀有度底、Lv 文字）。</summary>
public sealed partial class TheaterAvatarItem(ImaginariumTheaterAvatar avatar) : ObservableObject
{
    public string? Url { get; } = avatar.Image;

    public int Rarity { get; } = avatar.Rarity;

    public string LevelText { get; } = $"Lv.{avatar.Level}";

    [ObservableProperty]
    private Bitmap? _icon;
}


/// <summary>辉彩祝福 / 奇妙助益 / 神秘收获的一格图标（32×32 + 悬浮说明）。</summary>
public sealed partial class TheaterBuffItem : ObservableObject
{
    public TheaterBuffItem(ImaginariumTheaterSplendourBuffItem buff)
    {
        Name = buff.Name ?? "";
        LevelText = $"Lv.{buff.Level}";
        IsDim = buff.Level == 0;
        Url = buff.Icon;
        TooltipLines = buff.LevelEffect?.Select(x => x.Desc ?? "").Where(x => x.Length > 0).ToList() ?? [];
    }

    public TheaterBuffItem(ImaginariumTheaterBuff buff)
    {
        Name = buff.Name ?? "";
        LevelText = "";
        IsDim = false;
        Url = buff.Icon;
        TooltipLines = string.IsNullOrWhiteSpace(buff.Desc) ? [] : [buff.Desc];
    }

    public string Name { get; }

    public string LevelText { get; }

    public bool IsDim { get; }

    public string? Url { get; }

    public List<string> TooltipLines { get; }

    public double IconOpacity => IsDim ? 0.3 : 1;

    [ObservableProperty]
    private Bitmap? _icon;
}
