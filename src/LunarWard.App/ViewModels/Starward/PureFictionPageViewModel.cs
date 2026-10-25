using LunarWard.App.Services;
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
using LunarWard.Core.GameRecord.StarRail.PureFiction;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 虚构叙事页 VM（Starward <c>Features/GameRecord/StarRail/PureFictionPage.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 版式与 <see cref="ForgottenHallPageViewModel"/> 同源（源码就是一个文件复制改出来的）：
/// 左侧「刷新 + 各期列表（日期 / 星数 / 星芒额外星）」，右侧「统计周期 → 出战统计卡 →
/// 每层卡片（名称 / 使用轮次 / 星数 + 总分 / 节点 1、2 的战斗时间与得分 + 阵容 / 节点 3）」，另外
/// 每层多了「总分」「节点得分」两组数字，快速通关的判断与占位图沿用忘却之庭的做法。
/// </para>
/// <para>
/// 接口只有「当期 / 上期」两档（<c>schedule_type=1|2</c>），进页依次拉取，任一成功就展示。
/// </para>
/// </remarks>
public sealed partial class PureFictionPageViewModel : StarwardPageViewModel
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly GameRecordClient _client;

    private readonly GameRecordRole _role;

    private bool _initialized;


    public PureFictionPageViewModel(PageContext context, GameRecordRole role) : base(context)
    {
        _role = role;
        _client = (role.GameBiz ?? "").EndsWith("_global", StringComparison.Ordinal)
            ? new HoyolabClient(Http)
            : new HyperionClient(Http);
        _ = InitializeAsync();
    }


    /// <summary>左侧列表（Starward <c>ListView_ForgottenHall</c>：当期 + 上期）。</summary>
    public ObservableCollection<PureFictionPeriodItem> PeriodList { get; } = [];

    [ObservableProperty]
    private PureFictionPeriodItem? _currentPeriod;

    partial void OnCurrentPeriodChanged(PureFictionPeriodItem? value) => ApplyCurrent(value);

    [ObservableProperty]
    private PureFictionInfo? _currentInfo;

    /// <summary>每层一张卡（Starward <c>CurrentPureFiction.AllFloorDetail</c>）。</summary>
    public ObservableCollection<PureFictionFloorItem> Floors { get; } = [];

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
            var infos = new List<PureFictionInfo>();
            Exception? error = null;
            foreach (var schedule in new[] { 1, 2 })
            {
                try
                {
                    var info = await _client.GetPureFictionInfoAsync(_role, schedule).ConfigureAwait(true);
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
                PeriodList.Add(new PureFictionPeriodItem(info));
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
            ErrorText = string.Format(Strings["PureFiction.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorText = string.Format(Strings["PureFiction.NetworkError"], ex.Message);
            ShowEmpty = true;
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(Strings["PureFiction.LoadFailed"], ex.Message);
            ShowEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }


    private void ApplyCurrent(PureFictionPeriodItem? item)
    {
        CurrentInfo = item?.Info;
        Floors.Clear();
        if (CurrentInfo?.AllFloorDetail is { Count: > 0 } floors)
        {
            foreach (var floor in floors)
            {
                Floors.Add(new PureFictionFloorItem(floor));
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
        var infos = new List<PureFictionInfo>();
        for (var i = 0; i < 2; i++)
        {
            var begin = new DateTimeOffset(now.Year, now.Month, now.Day, 4, 0, 0, now.Offset).AddDays(-14 * i);
            var info = new PureFictionInfo
            {
                ScheduleId = 200 + i,
                Uid = _role.Uid,
                BeginTime = begin.DateTime,
                EndTime = begin.AddDays(14).DateTime,
                StarNum = 12 - i * 2,
                ExtraStarNum = i == 0 ? 3 : 0,
                MaxFloor = i == 0 ? "4" : "3",
                BattleNum = 6,
                HasData = true,
                Metas =
                [
                    new PureFictionMeta
                    {
                        ScheduleId = 200 + i,
                        BeginTime = begin.DateTime,
                        EndTime = begin.AddDays(14).DateTime,
                        Name = i == 0 ? "虚构叙事·自证预言" : "虚构叙事·庸与神的冠冕",
                    },
                ],
                AllFloorDetail = BuildDemoFloors(now, i),
            };
            infos.Add(info);
        }

        PeriodList.Clear();
        foreach (var info in infos)
        {
            PeriodList.Add(new PureFictionPeriodItem(info));
        }

        ShowEmpty = false;
        CurrentPeriod = PeriodList[0];
    }


    private static List<PureFictionFloorDetail> BuildDemoFloors(DateTimeOffset now, int period)
    {
        var floors = new List<PureFictionFloorDetail>();
        for (var i = 0; i < 4; i++)
        {
            var time = now.AddDays(-13 + i).UtcDateTime;
            // 第 3 张演示「快速通关」：源码同样以 IsFast 隐藏节点与总分。
            var quickCleared = i == 2;
            var node1 = new PureFictionNode
            {
                ChallengeTime = time,
                Score = 40000 - i * 5000,
                Avatars = quickCleared ? [] : BuildDemoAvatars(i, 0),
            };
            var node2 = new PureFictionNode
            {
                ChallengeTime = time.AddMinutes(2),
                Score = 32000 - i * 3000,
                Avatars = quickCleared ? [] : BuildDemoAvatars(i, 4),
            };
            floors.Add(new PureFictionFloorDetail
            {
                Name = $"虚构叙事 {i + 1}",
                RoundNum = 4 - i,
                StarNum = i == 0 ? 3 : 2,
                IsFast = quickCleared,
                MazeId = 2000 + i,
                Node1 = node1,
                Node2 = node2,
                // 第 1 张卡带节点 3（星芒模式），用来核对「节点 3」条幅。
                Node3 = i == 0
                    ? new PureFictionNode
                    {
                        ChallengeTime = time.AddMinutes(6),
                        Score = 26000,
                        Avatars = BuildDemoAvatars(i, 8),
                    }
                    : null,
            });
        }

        return floors;
    }


    private static List<PureFictionAvatar> BuildDemoAvatars(int floor, int offset)
    {
        var avatars = new List<PureFictionAvatar>();
        for (var i = 0; i < 4; i++)
        {
            avatars.Add(new PureFictionAvatar
            {
                Id = 2000 + floor * 10 + offset + i,
                Level = 80,
                Rarity = i % 2 == 0 ? 5 : 4,
                Rank = i == 0 ? 2 : 0,
                Element = "Quantum",
                Icon = "",
            });
        }

        return avatars;
    }
}


/// <summary>左侧列表的一期（Starward <c>PureFictionInfo</c> 的日期 + 星数行）。</summary>
public sealed class PureFictionPeriodItem
{
    public PureFictionPeriodItem(PureFictionInfo info)
    {
        Info = info;
    }

    public PureFictionInfo Info { get; }

    public string DateText => Info.BeginTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string StarNumText => Info.StarNum.ToString(CultureInfo.InvariantCulture);

    public string ExtraStarNumText => Info.ExtraStarNum.ToString(CultureInfo.InvariantCulture);

    public bool HasExtraStar => Info.ExtraStarNum > 0;
}


/// <summary>一层卡（Starward <c>PureFictionFloorDetail</c> + 三个节点的头像集合与得分）。</summary>
public sealed partial class PureFictionFloorItem : ObservableObject
{
    public PureFictionFloorItem(PureFictionFloorDetail detail)
    {
        Detail = detail;
        Node1Avatars = Build(detail.Node1);
        Node2Avatars = Build(detail.Node2);
        Node3Avatars = Build(detail.Node3);
        TotalScoreText = detail.TotalScore.ToString(CultureInfo.InvariantCulture);
        Node1ScoreText = detail.Node1?.Score.ToString(CultureInfo.InvariantCulture) ?? "";
        Node2ScoreText = detail.Node2?.Score.ToString(CultureInfo.InvariantCulture) ?? "";
        Node3ScoreText = detail.Node3?.Score.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    public PureFictionFloorDetail Detail { get; }

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

    public string TotalScoreText { get; }

    public string Node1ScoreText { get; }

    public string Node2ScoreText { get; }

    public string Node3ScoreText { get; }

    public string Node1TimeText => Detail.Node1?.ChallengeTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";

    public string Node2TimeText => Detail.Node2?.ChallengeTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";

    public string Node3TimeText => Detail.Node3?.ChallengeTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";

    public List<PureFictionAvatarItem> Node1Avatars { get; }

    public List<PureFictionAvatarItem> Node2Avatars { get; }

    public List<PureFictionAvatarItem> Node3Avatars { get; }

    /// <summary>XAML 文案索引器（层卡片在 ItemsControl 里，取不到页面 DataContext）。</summary>
    public Localization.AppStrings Strings => Localization.AppStrings.Instance;


    private static List<PureFictionAvatarItem> Build(PureFictionNode? node)
        => node?.Avatars is { Count: > 0 } avatars
            ? avatars.Select(x => new PureFictionAvatarItem(x)).ToList()
            : [];
}


/// <summary>出战角色卡（Starward 的 72×84 头像 + 稀有度底 + 星魂 + Lv）。</summary>
public sealed partial class PureFictionAvatarItem : ObservableObject
{
    public PureFictionAvatarItem(PureFictionAvatar avatar)
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
