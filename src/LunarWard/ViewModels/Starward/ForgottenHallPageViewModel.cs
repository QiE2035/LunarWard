using LunarWard.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.GameRecord;
using LunarWard.Core.GameRecord.StarRail.ForgottenHall;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 忘却之庭页 VM（Starward <c>Features/GameRecord/StarRail/ForgottenHallPage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 左侧是「刷新 + 总览」按钮与各期列表（日期 + 星数 + 额外星数），右侧是
/// 「统计周期 → 出战统计卡 → 每层卡片（名称 / 使用轮次 / 星数 / 节点 1、2 阵容与战斗时间 / 快速通关）」。
/// </para>
/// <para>
/// 旧接口只提供「当期 / 上期」两档（<c>schedule_type=1|2</c>），Starward 从 SQLite 读全部历史；
/// 这里进页依次拉当期与上期，任一成功就展示。刷新按钮重拉。
/// 星星图标、星芒额外星、快速通关配图都没有本地 PNG 素材，用字形 + 同色占位（见 XAML 注释）。
/// </para>
/// </remarks>
public sealed partial class ForgottenHallPageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    private bool _initialized;


    public ForgottenHallPageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _ = InitializeAsync();
    }


    /// <summary>左侧列表（Starward <c>ListView_PageList</c>：当期 + 上期）。</summary>
    public ObservableCollection<ForgottenHallPeriodItem> PeriodList { get; } = [];

    [ObservableProperty]
    private ForgottenHallPeriodItem? _currentPeriod;

    partial void OnCurrentPeriodChanged(ForgottenHallPeriodItem? value) => ApplyCurrent(value);

    [ObservableProperty]
    private ForgottenHallInfo? _currentInfo;

    /// <summary>每层一张卡（Starward <c>CurrentForgottenHall.AllFloorDetail</c>）。</summary>
    public ObservableCollection<ForgottenHallFloorItem> Floors { get; } = [];

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorText;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    public bool HasData => CurrentInfo is not null;

    public string PeriodText => CurrentInfo is { } info
        ? string.Format(
            Strings["SpiralAbyss.PeriodRange"],
            info.BeginTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
            info.EndTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture))
        : "";

    /// <summary>「统计周期」行右侧的期名（Starward <c>CurrentForgottenHall.Name</c>）。</summary>
    public string PeriodName => CurrentInfo?.Name ?? "";

    public string StarNumText => CurrentInfo?.StarNum.ToString(CultureInfo.InvariantCulture) ?? "";

    public string ExtraStarNumText => CurrentInfo?.ExtraStarNum.ToString(CultureInfo.InvariantCulture) ?? "";

    public bool HasExtraStar => (CurrentInfo?.ExtraStarNum ?? 0) > 0;

    public string MaxFloorText => CurrentInfo?.MaxFloor ?? "";

    public string BattleNumText => CurrentInfo?.BattleNum.ToString(CultureInfo.InvariantCulture) ?? "";


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
            var selected = CurrentPeriod?.Info.ScheduleId;
            var infos = new List<ForgottenHallInfo>();
            Exception? error = null;
            foreach (var schedule in new[] { 1, 2 })
            {
                try
                {
                    var info = await _client.GetForgottenHallInfoAsync(_role, schedule).ConfigureAwait(true);
                    if (info is not null)
                    {
                        infos.Add(info);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    error ??= ex;
                }
            }

            if (infos.Count == 0 && error is not null)
            {
                throw error;
            }

            PeriodList.Clear();
            foreach (var info in infos
                         .OrderByDescending(x => x.BeginTime)
                         .ThenByDescending(x => x.ScheduleId))
            {
                PeriodList.Add(new ForgottenHallPeriodItem(info));
            }

            ShowEmpty = PeriodList.Count == 0;
            if (ShowEmpty)
            {
                CurrentPeriod = null;
                CurrentInfo = null;
                Floors.Clear();
                return;
            }

            CurrentPeriod = PeriodList.FirstOrDefault(x => x.Info.ScheduleId == selected) ?? PeriodList[0];
        }
        catch (miHoYoApiException ex)
        {
            ErrorText = string.Format(Strings["ForgottenHall.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorText = string.Format(Strings["ForgottenHall.NetworkError"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["ForgottenHall.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }


    private void ApplyCurrent(ForgottenHallPeriodItem? item)
    {
        CurrentInfo = item?.Info;
        Floors.Clear();
        if (CurrentInfo?.AllFloorDetail is { Count: > 0 } floors)
        {
            foreach (var floor in floors)
            {
                Floors.Add(new ForgottenHallFloorItem(floor));
            }

            _ = LoadAvatarsAsync();
        }

        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(PeriodText));
        OnPropertyChanged(nameof(PeriodName));
        OnPropertyChanged(nameof(StarNumText));
        OnPropertyChanged(nameof(ExtraStarNumText));
        OnPropertyChanged(nameof(HasExtraStar));
        OnPropertyChanged(nameof(MaxFloorText));
        OnPropertyChanged(nameof(BattleNumText));
    }


    /// <summary>下载角色头像（失败只保留占位图标，不影响文字）。</summary>
    private async Task LoadAvatarsAsync()
    {
        var assets = Shell.Assets;
        foreach (var floor in Floors)
        {
            foreach (var avatar in floor.Node1Avatars.Concat(floor.Node2Avatars).Concat(floor.Node3Avatars))
            {
                if (string.IsNullOrWhiteSpace(avatar.Url))
                {
                    continue;
                }

                var bitmap = await assets.BitmapAsync(avatar.Url).ConfigureAwait(true);
                if (bitmap is not null)
                {
                    avatar.Icon = bitmap;
                }
            }
        }
    }


    /// <summary>
    /// 自检/截图夹具（<c>LUNARWARD_TOOLBOX_DEMO=1</c>）：不联网，给页面灌两期演示数据
    /// （对应 Starward 的 x:DesignInstance）。正常启动不会走到这里。
    /// </summary>
    private void ApplyDemo()
    {
        var now = DateTimeOffset.Now;
        var infos = new List<ForgottenHallInfo>();
        for (var i = 0; i < 2; i++)
        {
            var begin = new DateTimeOffset(now.Year, now.Month, now.Day, 4, 0, 0, now.Offset).AddDays(-14 * i);
            var info = new ForgottenHallInfo
            {
                ScheduleId = 100 + i,
                Uid = _role.Uid,
                BeginTime = begin.DateTime,
                EndTime = begin.AddDays(14).DateTime,
                StarNum = 30 - i * 3,
                ExtraStarNum = i == 0 ? 6 : 3,
                MaxFloor = i == 0 ? "12" : "11",
                BattleNum = 6,
                HasData = true,
                Metas =
                [
                    new ForgottenHallMeta
                    {
                        ScheduleId = 100 + i,
                        BeginTime = begin.DateTime,
                        EndTime = begin.AddDays(14).DateTime,
                        Name = i == 0 ? "混沌回忆·十二层" : "混沌回忆·十一层",
                    },
                ],
                AllFloorDetail = BuildDemoFloors(now, i),
            };
            infos.Add(info);
        }

        PeriodList.Clear();
        foreach (var info in infos)
        {
            PeriodList.Add(new ForgottenHallPeriodItem(info));
        }

        ShowEmpty = false;
        CurrentPeriod = PeriodList[0];
    }


    private static List<ForgottenHallFloorDetail> BuildDemoFloors(DateTimeOffset now, int period)
    {
        var floors = new List<ForgottenHallFloorDetail>();
        for (var i = 0; i < 4; i++)
        {
            var starNum = i == 0 ? 3 : 2;
            var time = now.AddDays(-13 + i).UtcDateTime;
            // 第 3 张演示「快速通关」：源码在节点都没有头像时判定 IsFast。
            var quickCleared = i == 2;
            floors.Add(new ForgottenHallFloorDetail
            {
                Name = $"回忆其{new[] { "十二", "十一", "十", "九" }[i]}",
                RoundNum = 5 - i,
                StarNum = starNum,
                IsChaos = true,
                MazeId = 1000 + i,
                Node1 = new ForgottenHallNode
                {
                    ChallengeTime = time,
                    Avatars = quickCleared ? [] : BuildDemoAvatars(i, 0),
                },
                Node2 = new ForgottenHallNode
                {
                    ChallengeTime = time.AddMinutes(2),
                    Avatars = quickCleared ? [] : BuildDemoAvatars(i, 4),
                },
            });
        }

        return floors;
    }


    private static List<ForgottenHallAvatar> BuildDemoAvatars(int floor, int offset)
    {
        var avatars = new List<ForgottenHallAvatar>();
        for (var i = 0; i < 4; i++)
        {
            avatars.Add(new ForgottenHallAvatar
            {
                Id = 1000 + floor * 10 + offset + i,
                Level = 80,
                Rarity = i % 2 == 0 ? 5 : 4,
                Rank = i == 0 ? 2 : 0,
                Element = "Ice",
                Icon = "",
            });
        }

        return avatars;
    }
}


/// <summary>左侧列表的一期（Starward <c>ForgottenHallInfo</c> 的日期 + 星数行）。</summary>
public sealed class ForgottenHallPeriodItem
{
    public ForgottenHallPeriodItem(ForgottenHallInfo info)
    {
        Info = info;
    }

    public ForgottenHallInfo Info { get; }

    public string DateText => Info.BeginTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string StarNumText => Info.StarNum.ToString(CultureInfo.InvariantCulture);

    public string ExtraStarNumText => Info.ExtraStarNum.ToString(CultureInfo.InvariantCulture);

    public bool HasExtraStar => Info.ExtraStarNum > 0;
}


/// <summary>一层卡（Starward <c>ForgottenHallFloorDetail</c> + 两个节点的头像集合）。</summary>
public sealed partial class ForgottenHallFloorItem : ObservableObject
{
    public ForgottenHallFloorItem(ForgottenHallFloorDetail detail)
    {
        Detail = detail;
        Node1Avatars = Build(detail.Node1);
        Node2Avatars = Build(detail.Node2);
        Node3Avatars = Build(detail.Node3);
    }

    public ForgottenHallFloorDetail Detail { get; }

    public string Name => Detail.Name ?? "";

    public string RoundNumText => Detail.RoundNum.ToString(CultureInfo.InvariantCulture);

    /// <summary>普通星（上限 3），XAML 用字形重复渲染。</summary>
    public string NormalStars => new('★', Math.Clamp(Detail.NormalStarNum, 0, 3));

    /// <summary>额外星（星芒），超过 3 星的部分。</summary>
    public string ExtraStars => new('★', Math.Max(0, Detail.ExtraStarNum));

    public bool HasExtraStar => Detail.ExtraStarNum > 0;

    /// <summary>快速通关（Starward 用 20004.png；本仓库无素材，只留文字）。</summary>
    public bool IsFast => Detail.IsFast;

    public bool ShowTeams => !Detail.IsFast;

    public bool HasNode3 => Detail.Node3 is not null;

    public string Node1TimeText => Detail.Node1?.ChallengeTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";

    public string Node2TimeText => Detail.Node2?.ChallengeTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";

    public string Node3TimeText => Detail.Node3?.ChallengeTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";

    public List<ForgottenHallAvatarItem> Node1Avatars { get; }

    public List<ForgottenHallAvatarItem> Node2Avatars { get; }

    public List<ForgottenHallAvatarItem> Node3Avatars { get; }

    /// <summary>XAML 文案索引器（层卡片在 ItemsControl 里，取不到页面 DataContext）。</summary>
    public Localization.AppStrings Strings => Localization.AppStrings.Instance;


    private static List<ForgottenHallAvatarItem> Build(ForgottenHallNode? node)
        => node?.Avatars is { Count: > 0 } avatars
            ? avatars.Select(x => new ForgottenHallAvatarItem(x)).ToList()
            : [];
}


/// <summary>出战角色卡（Starward 的 72×84 头像 + 稀有度底 + 星魂 + Lv）。</summary>
public sealed partial class ForgottenHallAvatarItem : ObservableObject
{
    public ForgottenHallAvatarItem(ForgottenHallAvatar avatar)
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
