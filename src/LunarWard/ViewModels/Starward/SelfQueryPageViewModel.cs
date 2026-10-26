using LunarWard.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.ViewModels.Starward;
using LunarWard.Views.Starward;
using LunarWard.Core;
using LunarWard.Core.SelfQuery;
using LunarWard.Launch.Database;

namespace LunarWard.ViewModels.Starward;

/// <summary>
/// 账号记录页（复刻 Starward <c>Features/SelfQuery/SelfQueryPage.xaml</c> + <c>.xaml.cs</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 页面逻辑（源码 <c>SelfQueryPage.xaml.cs</c>）：左侧是 UID 选择 + 每种类型的收支统计
/// （<c>TypeStatsList</c>），右侧是选中类型的月份下拉、本月收支，以及该月的明细三件套
/// （原神/星穹铁道/绝区零各一个 ListView）。
/// </para>
/// <para>
/// 与源码的差异只有持久化：Starward 走 SQLite/Dapper，LunarWard 没有数据库层，
/// 用 <see cref="LunarWard.Launch.Database.SelfQueryLogStore"/>（Starward 同款 SQLite 表）承载同样的「按 uid + type + 月份存取」语义。
/// </para>
/// </remarks>
public sealed partial class SelfQueryPageViewModel : StarwardPageViewModel
{


    private readonly SelfQueryLogStore _store;

    private readonly SelfQueryService _service;

    /// <summary>本页游戏类型：<c>hk4e</c> / <c>hkrpg</c> / <c>nap</c>（源码 <c>CurrentGameBiz.ToGame()</c>）。</summary>
    private readonly string _game;

    private readonly GameBiz _gameBiz;

    private readonly CancellationTokenSource _tokenSource = new();



    public SelfQueryPageViewModel(PageContext context) : base(context)
    {
        var biz = Profile.Profile.Game switch
        {
            "starrail" => "hkrpg",
            "zzz" => "nap",
            _ => "hk4e",
        };
        var server = string.Equals(Profile.Profile.Edition, "Global", StringComparison.OrdinalIgnoreCase) ? "global" : "cn";
        _gameBiz = new GameBiz(biz + "_" + server);
        _game = _gameBiz.Game;
        _store = SelfQueryLogStore.OpenDefault(Services.Paths.DataHome);
        _service = new SelfQueryService(_store);
    }



    /// <summary>当前游戏的游戏图标（源码 <c>GameIcon</c>；LunarWard 没有这张素材，用字形占位）。</summary>
    public string GameIconGlyph => _game switch
    {
        "hkrpg" => "\uE7FC",
        "nap" => "\uE8B8",
        _ => "\uE8A9",
    };


    public bool IsGenshin => _game == "hk4e";

    public bool IsStarRail => _game == "hkrpg";

    public bool IsZZZ => _game == "nap";


    // ── 用户信息 ──────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQueryUserInfo))]
    private SelfQueryUserInfo? _queryUserInfo;

    public bool HasQueryUserInfo => QueryUserInfo is not null;


    // ── UID ───────────────────────────────────────────────────────────

    /// <summary>下拉里的 UID 列表（源码 <c>UidList</c>）。</summary>
    public ObservableCollection<long> UidList { get; } = new();


    [ObservableProperty]
    private long? _selectUid;


    partial void OnSelectUidChanged(long? value)
    {
        LoadQueryTypeStats();
    }


    // ── 类型统计 ──────────────────────────────────────────────────────

    /// <summary>左侧类型统计列表（源码 <c>TypeStatsList</c>）。</summary>
    public ObservableCollection<QueryTypeStats> TypeStatsList { get; } = new();


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteConfirmText))]
    private QueryTypeStats? _selectTypeStats;


    partial void OnSelectTypeStatsChanged(QueryTypeStats? value)
    {
        if (value is not null)
        {
            TypeStatsChanged(value.Type);
        }
    }


    // ── 月份 / 本月统计 ───────────────────────────────────────────────

    /// <summary>选中类型的月份列表（源码 <c>TypeStatsMonthList</c>）。</summary>
    public ObservableCollection<string> TypeStatsMonthList { get; } = new();


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteConfirmText))]
    private string? _selectTypeStatsMonth;


    partial void OnSelectTypeStatsMonthChanged(string? value)
    {
        if (value is null)
        {
            GenshinQueryItemList.Clear();
            StarRailQueryItemList.Clear();
            ZZZQueryItemList.Clear();
            return;
        }
        LoadMonthQueryItems(SelectTypeStats?.Type ?? 0, value);
    }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthAddText))]
    private long _monthAddNum;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthSubText))]
    private long _monthSubNum;

    /// <summary>本月收入（源码 <c>SelfQueryStatsNumberConverter</c>：正数带 +）。</summary>
    public string MonthAddText => FormatNumber(MonthAddNum);

    /// <summary>本月支出（源码同一转换器：负数原样、0 显示 -）。</summary>
    public string MonthSubText => FormatNumber(MonthSubNum);


    /// <summary>源码 <c>SelfQueryStatsNumberConverter</c>：正数 +N、负数原样、0 显示 "-"。</summary>
    private static string FormatNumber(long num) => num switch
    {
        > 0 => "+" + num,
        < 0 => num.ToString(CultureInfo.InvariantCulture),
        _ => "-",
    };


    // ── 明细列表 ──────────────────────────────────────────────────────

    public ObservableCollection<GenshinQueryItem> GenshinQueryItemList { get; } = new();

    public ObservableCollection<StarRailQueryItem> StarRailQueryItemList { get; } = new();

    public ObservableCollection<ZZZQueryItem> ZZZQueryItemList { get; } = new();


    /// <summary>页面进入（源码 <c>OnLoaded</c>：延迟一帧再读，等界面先画出来）。</summary>
    public async Task ActivateAsync()
    {
        await Task.Delay(16);
        LoadUidList();
        if (CaptureMode.SelfQuerySelect)
        {
            // 自检专用：源码里选中/月份都由用户点选（见 SelfQueryPage.xaml.cs:87-125），
            // 截图核对右侧明细时必须先选一行，这里只在 --selfquery-select 下做。
            SelectTypeStats = TypeStatsList.FirstOrDefault();
        }
    }


    /// <summary>源码 <c>OnUnloaded</c>：清空列表并取消在途请求。</summary>
    public override void Dispose()
    {
        _tokenSource.Cancel();
        GenshinQueryItemList.Clear();
        StarRailQueryItemList.Clear();
        ZZZQueryItemList.Clear();
        QueryUserInfo = null;
        TypeStatsList.Clear();
        TypeStatsMonthList.Clear();
        base.Dispose();
    }





    // ── 载入 ──────────────────────────────────────────────────────────

    /// <summary>源码 <c>LoadUidList</c>。</summary>
    private void LoadUidList()
    {
        try
        {
            UidList.Clear();
            foreach (var uid in _game switch
            {
                "hkrpg" => _store.GetStarRailUids(),
                "nap" => _store.GetZZZUids(),
                _ => _store.GetGenshinUids(),
            })
            {
                UidList.Add(uid);
            }

            var info = _service.UserInfo;
            if (info is not null && info.GameBiz.ToGame() == _gameBiz.ToGame())
            {
                QueryUserInfo = info;
                if (!UidList.Contains(info.Uid))
                {
                    UidList.Add(info.Uid);
                }
                SelectUid = info.Uid;
            }
            else
            {
                _service.Reset();
                if (UidList.Count > 0)
                {
                    SelectUid = UidList[0];
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Load uid list", ex);
        }
    }


    /// <summary>源码 <c>LoadQueryTypeStats</c>：类型顺序原神 {1,2,3,6,7,4,5}、星铁 1..5、绝区零 1..6。</summary>
    private void LoadQueryTypeStats()
    {
        try
        {
            if (SelectUid is null or 0)
            {
                return;
            }
            long uid = SelectUid.Value;
            TypeStatsList.Clear();
            if (IsGenshin)
            {
                foreach (int i in new[] { 1, 2, 3, 6, 7, 4, 5 })
                {
                    var type = (GenshinQueryType)i;
                    var (add, sub) = _store.GetGenshinQueryItemsNumSum(uid, type);
                    TypeStatsList.Add(new QueryTypeStats
                    {
                        Type = i,
                        IconGlyph = TypeToGlyph(type),
                        Add = add,
                        Sub = sub,
                        Name = Strings["SelfQuery.Type." + type.ToString()],
                    });
                }
            }
            if (IsStarRail)
            {
                foreach (int i in Enumerable.Range(1, 5))
                {
                    var type = (StarRailQueryType)i;
                    var (add, sub) = _store.GetStarRailQueryItemsNumSum(uid, type);
                    TypeStatsList.Add(new QueryTypeStats
                    {
                        Type = i,
                        IconGlyph = TypeToGlyph(type),
                        Add = add,
                        Sub = sub,
                        Name = Strings["SelfQuery.Type." + type.ToString()],
                    });
                }
            }
            if (IsZZZ)
            {
                foreach (int i in Enumerable.Range(1, 6))
                {
                    var type = (ZZZQueryType)i;
                    var (add, sub) = _store.GetZZZQueryItemsNumSum(uid, type);
                    TypeStatsList.Add(new QueryTypeStats
                    {
                        Type = i,
                        IconGlyph = TypeToGlyph(type),
                        Add = add,
                        Sub = sub,
                        Name = Strings["SelfQuery.Type." + type.ToString()],
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Load query type stats", ex);
        }
    }


    /// <summary>源码 <c>TypeStatsChanged</c>：刷新月份列表并选中最新一个月。</summary>
    private void TypeStatsChanged(int type)
    {
        try
        {
            MonthAddNum = 0;
            MonthSubNum = 0;
            if (SelectUid is null)
            {
                return;
            }
            long uid = SelectUid.Value;
            var months = _game switch
            {
                "hkrpg" => _store.GetStarRailMonths(uid, (StarRailQueryType)type),
                "nap" => _store.GetZZZMonths(uid, (ZZZQueryType)type),
                _ => _store.GetGenshinMonths(uid, (GenshinQueryType)type),
            };
            TypeStatsMonthList.Clear();
            foreach (var month in months)
            {
                TypeStatsMonthList.Add(month);
            }
            SelectTypeStatsMonth = TypeStatsMonthList.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Log.Error("TypeStatsChanged", ex);
        }
    }


    /// <summary>源码 <c>LoadMonthQueryItems</c>。</summary>
    private void LoadMonthQueryItems(int type, string month)
    {
        try
        {
            if (SelectUid is null)
            {
                return;
            }
            long uid = SelectUid.Value;
            if (IsGenshin)
            {
                GenshinQueryItemList.Clear();
                foreach (var item in _store.GetGenshinQueryItems(uid, (GenshinQueryType)type, month))
                {
                    GenshinQueryItemList.Add(item);
                }
                MonthAddNum = GenshinQueryItemList.Where(x => x.AddNum > 0).Sum(x => x.AddNum);
                MonthSubNum = GenshinQueryItemList.Where(x => x.AddNum < 0).Sum(x => x.AddNum);
            }
            if (IsStarRail)
            {
                StarRailQueryItemList.Clear();
                foreach (var item in _store.GetStarRailQueryItems(uid, (StarRailQueryType)type, month))
                {
                    StarRailQueryItemList.Add(item);
                }
                MonthAddNum = StarRailQueryItemList.Where(x => x.AddNum > 0).Sum(x => x.AddNum);
                MonthSubNum = StarRailQueryItemList.Where(x => x.AddNum < 0).Sum(x => x.AddNum);
            }
            if (IsZZZ)
            {
                ZZZQueryItemList.Clear();
                var list = _store.GetZZZQueryItems(uid, (ZZZQueryType)type, month);
                if (type == (int)ZZZQueryType.PurchaseGift)
                {
                    foreach (var item in list)
                    {
                        item.Reason = item.ItemName;
                    }
                }
                foreach (var item in list)
                {
                    ZZZQueryItemList.Add(item);
                }
                MonthAddNum = ZZZQueryItemList.Where(x => x.AddNum > 0).Sum(x => x.AddNum);
                MonthSubNum = ZZZQueryItemList.Where(x => x.AddNum < 0).Sum(x => x.AddNum);
            }
        }
        catch (Exception ex)
        {
            Log.Error("LoadMonthQueryItems", ex);
        }
    }





    // ── 命令 ──────────────────────────────────────────────────────────

    /// <summary>源码 <c>InputURLCommand</c>：对话框由视图负责，确认后回调 <see cref="InitializeByUrlAsync"/>。</summary>
    [RelayCommand]
    private void InputURL() => InputURLRequested?.Invoke();


    /// <summary>视图的「输入 URL」对话框确认后调用。</summary>
    public async Task<bool> InitializeByUrlAsync(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }
            QueryUserInfo = await _service.InitializeAsync(url, _gameBiz, _tokenSource.Token);
            if (!UidList.Contains(QueryUserInfo.Uid))
            {
                UidList.Add(QueryUserInfo.Uid);
            }
            SelectUid = QueryUserInfo.Uid;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Input url", ex);
            return false;
        }
    }


    /// <summary>视图订阅它来弹「输入 URL」对话框（源码里是 <c>InputURLAsync</c> 内的 ContentDialog）。</summary>
    public event Action? InputURLRequested;


    /// <summary>视图里单个类型的刷新按钮（源码 <c>Button_UpdateSelfQueryItems_Click</c>）。</summary>
    [RelayCommand]
    private async Task UpdateItemAsync(QueryTypeStats? stats)
    {
        if (stats is null || stats.IsUpdating)
        {
            return;
        }
        await RunUpdateAsync(stats, all: false);
    }


    /// <summary>源码 <c>UpdateAllQueryItemsCommand</c>：删除涉及月份后整批重拉。</summary>
    [RelayCommand]
    private async Task UpdateAllQueryItemsAsync()
    {
        if (SelectTypeStats is not { IsUpdating: false } stats)
        {
            return;
        }
        await RunUpdateAsync(stats, all: true);
    }


    private async Task RunUpdateAsync(QueryTypeStats stats, bool all)
    {
        try
        {
            stats.IsUpdating = true;
            if (!await EnsureInitializedAsync())
            {
                return;
            }
            var progress = new Progress<int>(i => stats.Page = i);
            var (add, sub) = _game switch
            {
                "hkrpg" => await _service.UpdateStarRailQueryItemsAsync((StarRailQueryType)stats.Type, progress, all, _tokenSource.Token),
                "nap" => await _service.UpdateZZZQueryItemsAsync((ZZZQueryType)stats.Type, progress, all, _tokenSource.Token),
                _ => await _service.UpdateGenshinQueryItemsAsync((GenshinQueryType)stats.Type, progress, all, _tokenSource.Token),
            };
            stats.Add = add;
            stats.Sub = sub;
            TypeStatsChanged(stats.Type);
        }
        catch (OperationCanceledException ex)
        {
            Log.Warn("Update self query items: " + ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error("Update self query items", ex);
        }
        finally
        {
            stats.IsUpdating = false;
        }
    }


    /// <summary>源码里每次更新前 <c>EnsureInitialized()</c>，失败则弹输入 URL。</summary>
    private async Task<bool> EnsureInitializedAsync()
    {
        try
        {
            _service.EnsureInitialized();
            return true;
        }
        catch (Exception)
        {
            InputURLRequested?.Invoke();
            await Task.CompletedTask;
            return false;
        }
    }


    /// <summary>源码 <c>DeleteCurrentMonthDataCommand</c>：确认框由视图负责，确认后调 <see cref="DeleteCurrentMonthAsync"/>。</summary>
    [RelayCommand]
    private void DeleteCurrentMonthData() => DeleteCurrentMonthRequested?.Invoke();


    public event Action? DeleteCurrentMonthRequested;


    /// <summary>删除选中类型的本月数据（源码里在 ContentDialog 确认后执行）。</summary>
    public void DeleteCurrentMonthAsync()
    {
        try
        {
            if (SelectUid is null || SelectTypeStats is null || SelectTypeStatsMonth is null)
            {
                return;
            }
            long uid = SelectUid.Value;
            int type = SelectTypeStats.Type;
            string month = SelectTypeStatsMonth;
            switch (_game)
            {
                case "hkrpg":
                    _store.DeleteStarRailMonth(uid, (StarRailQueryType)type, month);
                    break;
                case "nap":
                    _store.DeleteZZZMonth(uid, (ZZZQueryType)type, month);
                    break;
                default:
                    _store.DeleteGenshinMonth(uid, (GenshinQueryType)type, month);
                    break;
            }
            TypeStatsChanged(type);
        }
        catch (Exception ex)
        {
            Log.Error("Delete records", ex);
        }
    }


    /// <summary>确认框正文（源码 <c>SelfQueryPage_DeleteThisMonthSData_DialogContent</c>）。</summary>
    public string DeleteConfirmText => string.Format(
        CultureInfo.CurrentCulture,
        Strings["SelfQuery.DeleteThisMonthDialogContent"],
        SelectTypeStats?.Name ?? "",
        SelectTypeStatsMonth ?? "");



    // ── 图标占位（源码 TypeToIcon 返回 PNG 资源，LunarWard 没有这些素材）────

    private static string TypeToGlyph(GenshinQueryType type) => type switch
    {
        GenshinQueryType.Crystal => "\uE8B7",
        GenshinQueryType.Primogem => "\uE7C3",
        GenshinQueryType.Resin => "\uE945",
        GenshinQueryType.Artifact => "\uE8B5",
        GenshinQueryType.Weapon => "\uE7A8",
        GenshinQueryType.BeyondCrystal => "\uE8B0",
        GenshinQueryType.BeyondGachaCoin => "\uE8EF",
        _ => "\uE946",
    };


    private static string TypeToGlyph(StarRailQueryType type) => type switch
    {
        StarRailQueryType.Stellar => "\uE8B7",
        StarRailQueryType.Dreams => "\uE7C3",
        StarRailQueryType.Relic => "\uE8B5",
        StarRailQueryType.Cone => "\uE7A8",
        StarRailQueryType.Power => "\uE945",
        _ => "\uE946",
    };


    private static string TypeToGlyph(ZZZQueryType type) => type switch
    {
        ZZZQueryType.Monochrome => "\uE8B7",
        ZZZQueryType.Ploychrome => "\uE7C3",
        ZZZQueryType.PurchaseGift => "\uE8B0",
        ZZZQueryType.Battery => "\uE945",
        ZZZQueryType.Engine => "\uE7A8",
        ZZZQueryType.Disk => "\uE8B5",
        _ => "\uE946",
    };

}





/// <summary>左侧一行类型统计（源码 <c>SelfQueryPage.TypeStats</c>，继承 ObservableObject）。</summary>
public sealed partial class QueryTypeStats : ObservableObject
{

    public int Type { get; set; }

    public string Name { get; set; } = "";

    /// <summary>图标（源码是 PNG 路径，这里用字形占位，见 ViewModel 的 TypeToGlyph 注释）。</summary>
    public string IconGlyph { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddText))]
    private long _add;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubText))]
    private long _sub;

    /// <summary>总收入（源码 <c>SelfQueryStatsNumberConverter</c>：正数 +N、负数原样、0 显示 "-"）。</summary>
    public string AddText => Format(Add);

    /// <summary>总支出（同一转换器）。</summary>
    public string SubText => Format(Sub);


    private static string Format(long num) => num switch
    {
        > 0 => "+" + num,
        < 0 => num.ToString(CultureInfo.InvariantCulture),
        _ => "-",
    };

    [ObservableProperty]
    private bool _isUpdating;

    /// <summary>当前翻到第几页（源码 <c>Page</c>，更新时盖在进度环上）。</summary>
    [ObservableProperty]
    private int _page;

}
