using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.ViewModels;
using LunarWard.App.Views.Starward;
using LunarWard.Games;
using LunarWard.Launch;
using LunarWard.Platform;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 抽卡记录页（复刻 Starward <c>Features/Gacha/GachaLogPage.xaml</c>）的数据侧。
/// </summary>
/// <remarks>
/// <para>
/// 页面结构照 GachaLogPage：44px 页头（标题 = <see cref="GachaTypeText"/>、UID ComboBox、
/// 更新记录、设置、Pane 按钮）+ SplitView（右侧 Pane = 材料统计）。
/// 卡池卡片对应 <c>GachaStatsCard</c>，见 <see cref="GachaPoolCardViewModel"/>。
/// </para>
/// <para>
/// 拉取、定位、统计全部复用既有实现：<see cref="GachaService.FindGachaUrl"/> /
/// <see cref="GachaService.FetchAsync"/> / <see cref="GachaService.ComputeStatistics"/>。
/// 页面 VM <b>不重算</b>统计口径，只把结果按 Starward 的 <c>GachaTypeStats</c> 整形。
/// </para>
/// <para>
/// 与 Starward 的已知差异（详见实现报告）：
/// 记录只在本页内存中（无数据库），所以没有「按时间段删除」；
/// <c>GachaRecord</c> 没有 <c>ItemId</c>，材料统计按名称分组、<c>up!</c> 不显示。
/// </para>
/// </remarks>
public sealed partial class GachaPageViewModel : StarwardPageViewModel
{
    private readonly GachaService _gacha = new();

    private readonly string _gameDirectory;

    private readonly string _gameBiz;

    private readonly string _gameKind;

    private IReadOnlyList<GachaRecord> _allRecords = [];

    /// <summary>「隐藏卡池」勾选项（空集合 = 显示全部，语义同 Starward GachaLogPage.xaml.cs:381-384）。</summary>
    private readonly HashSet<int> _selectedPools = [];

    private bool _suppressUidRebuild;

    private bool _suppressBannerCallback;

    private bool _disposed;


    public GachaPageViewModel(PageContext context) : base(context)
    {
        _gameDirectory = ProfileConfig.ExpandPath(Profile.Profile.Path);
        _gameKind = Profile.Profile.Game;

        var definition = Games.GameCatalog.Get(
            Profile.Profile.Game,
            string.Equals(Profile.Profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
                ? Games.GameEdition.Global
                : Games.GameEdition.China);
        _gameBiz = definition?.GameBiz ?? "hk4e_cn";

        Pools = [];
        ItemStats = [];
        UidList = [];
        BannerOptions = [];

        // 打开页面只定位链接，不自动拉取（避免一进页面就打网络，与旧窗口行为一致）
        var url = GachaService.FindGachaUrl(_gameDirectory, new LunarWard.Core.GameBiz(_gameBiz), out var error);
        GachaUrl = url ?? "";

        // 定位失败时只说一次：错误条已经写清原因与下一步，状态行留空，避免两行说同一件事。
        StatusText = url is null ? "" : Strings["Gacha.UrlFound"];
        ErrorText = url is null ? NormalizeError(error) : null;
    }


    /// <summary>
    /// 把服务层的中文错误整成一句完整的话。
    /// </summary>
    /// <remarks>
    /// <see cref="GachaService.FindGachaUrl"/> 在游戏目录为空时会给出
    /// <c>"游戏目录不存在："</c>（后面拼空路径），界面上是个断掉的句子，
    /// 这里换成带操作指引的本地化文案。
    /// </remarks>
    private string NormalizeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(_gameDirectory) || !Directory.Exists(_gameDirectory))
        {
            return Strings["Gacha.NoGameDirectory"];
        }

        return string.IsNullOrWhiteSpace(error) ? Strings["Gacha.NoUrl"] : error;
    }


    // ── 页头 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 页头标题 = Starward <c>GachaLogService.GetGachaLogText(CurrentGameBiz)</c>：
    /// 原神「祈愿记录」、绝区零「调频记录」。
    /// </summary>
    public string GachaTypeText => _gameKind == "zzz"
        ? Strings["Gacha.LogTitle.Zzz"]
        : Strings["Gacha.LogTitle.Genshin"];

    /// <summary>抽卡链接（来自游戏网页缓存，可手工粘贴）。</summary>
    [ObservableProperty]
    private string _gachaUrl = "";

    /// <summary>次要色状态行（读取进度 / 已定位 / 导出结果）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    [NotifyPropertyChangedFor(nameof(ShowStatusArea))]
    private string _statusText = "";

    /// <summary>状态行是否有内容（空时不占高度）。</summary>
    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);

    [ObservableProperty]
    private string? _errorText;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    partial void OnErrorTextChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowStatusArea));
    }

    /// <summary>状态行或错误条任一有内容 → 页头下方的那一块才占高度。</summary>
    public bool ShowStatusArea => HasStatus || HasError;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>右侧材料统计 Pane 是否展开（Starward <c>SplitView_Content.IsPaneOpen</c>）。</summary>
    [ObservableProperty]
    private bool _isPaneOpen;

    /// <summary>当前选中的 UID（Starward <c>SelectUid</c>；Starward 从数据库取，这里从本次记录取）。</summary>
    [ObservableProperty]
    private long? _selectUid;

    partial void OnSelectUidChanged(long? value)
    {
        if (!_suppressUidRebuild)
        {
            Rebuild();
        }
    }


    // ── 数据 ──────────────────────────────────────────────────────────

    /// <summary>卡池统计卡（数据来自 <see cref="GachaService.ComputeStatistics"/>）。</summary>
    public ObservableCollection<GachaPoolCardViewModel> Pools { get; }

    /// <summary>右侧 Pane 的材料统计（Starward <c>GachaItemStats</c>）。</summary>
    public ObservableCollection<GachaItemStatsViewModel> ItemStats { get; }

    /// <summary>页头 UID 下拉（Starward <c>UidList</c>）。</summary>
    public ObservableCollection<long> UidList { get; }

    /// <summary>「隐藏卡池」Flyout 的候选项。</summary>
    public ObservableCollection<GachaBannerViewModel> BannerOptions { get; }

    public bool HasPools => Pools.Count > 0;

    public bool HasItemStats => ItemStats.Count > 0;

    public bool HasBannerOptions => BannerOptions.Count > 0;

    public bool HasUids => UidList.Count > 0;

    /// <summary>是否已经拉到任何记录（Starward 用 <c>GachaTypeStats</c> 是否存在判断）。</summary>
    public bool HasRecords => _allRecords.Count > 0;

    /// <summary>没有任何记录 → 整页居中空态（Starward StackPanel_Emoji）。</summary>
    public bool ShowEmptyState => !HasRecords;


    // ── 设置 Flyout 文案 / 占位状态 ────────────────────────────────────

    /// <summary>旧版导出格式名（Starward GachaLogPage.xaml.cs:59-70）：原神 UIGF v3.0，绝区零 JSON。</summary>
    public string LegacyFormatName => _gameKind == "zzz" ? "JSON" : "UIGF v3.0";

    /// <summary>「从 UIGF v3.0 导入」按钮文字（Starward <c>LegacyImportText</c>）。</summary>
    public string LegacyImportText => string.Format(Strings["Gacha.ImportFromLegacy"], LegacyFormatName);

    /// <summary>抽卡语言（Starward <c>GachaLanguage</c> 默认跟随链接的 lang）。</summary>
    public string GachaLanguage => "zh-cn";

    /// <summary>
    /// UIGF v4 导入导出是否可用。
    /// </summary>
    /// <remarks>
    /// <c>LunarWard.Core</c> 目前还没有 UIGF 读写实现（见 <c>docs/05-ROADMAP.md</c>），
    /// 按契约把入口做成禁用并注明"待接"，不伪造功能。
    /// </remarks>
    public bool IsUigfAvailable => false;

    public string UigfHint => Strings["Gacha.UigfPending"];

    /// <summary>UIGF/JSON 导入是否可用（Starward 走 ImportGachaLogCommand；本仓库还没有实现）。</summary>
    public bool IsImportAvailable => false;

    public string ImportHint => Strings["Gacha.ImportPending"];

    /// <summary>抽卡语言切换是否可用（需要 LunarWard.Core 透传 lang 参数）。</summary>
    public bool IsLanguageChangeAvailable => false;

    public string LanguageHint => Strings["Gacha.LanguagePending"];


    // ── 主操作 ────────────────────────────────────────────────────────

    [RelayCommand]
    private void Relocate()
    {
        var url = GachaService.FindGachaUrl(_gameDirectory, new LunarWard.Core.GameBiz(_gameBiz), out var error);
        if (url is null)
        {
            // 清掉上一轮的"已找到"状态，避免成功提示和错误条同时出现
            StatusText = "";
            ErrorText = NormalizeError(error);
            return;
        }

        GachaUrl = url;
        ErrorText = null;
        StatusText = Strings["Gacha.UrlRelocated"];
    }


    [RelayCommand]
    private async Task FetchAsync()
    {
        if (string.IsNullOrWhiteSpace(GachaUrl))
        {
            ErrorText = Strings["Gacha.NoUrlHint"];
            return;
        }

        IsBusy = true;
        ErrorText = null;
        StatusText = Strings["Gacha.Fetching"];

        var pages = new Dictionary<string, int>(StringComparer.Ordinal);
        var progress = new Progress<(string Pool, int Page)>(p =>
        {
            pages[p.Pool] = p.Page;
            StatusText = string.Join("  ", pages.Select(kv => $"{kv.Key} #{kv.Value}"));
        });

        try
        {
            var records = await _gacha.FetchAsync(_gameKind, GachaUrl, progress).ConfigureAwait(true);
            if (_disposed)
            {
                return;
            }

            _allRecords = records;
            RebuildUidList();
            Rebuild();
            StatusText = string.Format(Strings["Gacha.FetchDone"], _allRecords.Count);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            Log.Warn($"抽卡记录读取失败：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }


    [RelayCommand]
    private void ExportJson()
    {
        if (_allRecords.Count == 0)
        {
            ErrorText = Strings["Gacha.NoExport"];
            return;
        }

        try
        {
            var dir = Path.Join(Services.Paths.DataHome, "gacha");
            Directory.CreateDirectory(dir);
            var path = Path.Join(dir, $"{_gameKind}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            Platform.AtomicFile.WriteAllText(path,
                System.Text.Json.JsonSerializer.Serialize(_allRecords,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    }));
            ErrorText = null;
            StatusText = string.Format(Strings["Gacha.Exported"], path);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    /// <summary>
    /// 打开网页缓存文件夹（Starward <c>DeleteGachaCacheFolderCommand</c>：
    /// 名字叫"删除"，实现其实是打开 <c>webCaches</c> 所在目录，让用户自己删）。
    /// </summary>
    [RelayCommand]
    private void OpenCacheFolder()
    {
        try
        {
            var path = LunarWard.Core.Gacha.GachaLogClient.GetWebCachesFolderPath(
                new LunarWard.Core.GameBiz(_gameBiz), _gameDirectory);
            if (!Directory.Exists(path))
            {
                ErrorText = Strings["Gacha.CacheFolderMissing"];
                return;
            }

            var info = new ProcessStartInfo { FileName = "xdg-open", UseShellExecute = false };
            info.ArgumentList.Add(path);
            using var process = Process.Start(info);
            ErrorText = null;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    /// <summary>Pane 按钮（Starward <c>OpenItemStatsPaneCommand</c>）：切换材料统计侧栏。</summary>
    [RelayCommand]
    private void ToggleItemStatsPane() => IsPaneOpen = !IsPaneOpen;


    /// <summary>删除记录 → 全部删除（记录只在内存里，清掉即回到空态）。</summary>
    [RelayCommand]
    private void ClearRecords()
    {
        _allRecords = [];
        _selectedPools.Clear();

        _suppressUidRebuild = true;
        UidList.Clear();
        SelectUid = null;
        _suppressUidRebuild = false;

        _suppressBannerCallback = true;
        BannerOptions.Clear();
        _suppressBannerCallback = false;

        Rebuild();
        ErrorText = null;
        StatusText = Strings["Gacha.RecordsCleared"];

        OnPropertyChanged(nameof(HasUids));
        OnPropertyChanged(nameof(HasBannerOptions));
    }


    /// <summary>剪贴板复制成功后的状态提示（剪贴板只能从视图层拿，见 GachaPage.axaml.cs）。</summary>
    public void NotifyUrlCopied()
    {
        ErrorText = null;
        StatusText = Strings["Gacha.CopyUrlDone"];
    }


    /// <summary>视图层操作失败时的统一入口。</summary>
    public void NotifyError(string message)
    {
        StatusText = "";
        ErrorText = message;
    }


    // ── 内部 ──────────────────────────────────────────────────────────

    private void RebuildUidList()
    {
        _suppressUidRebuild = true;
        UidList.Clear();
        foreach (var uid in _allRecords
                     .Select(r => r.Uid)
                     .Where(uid => uid > 0)
                     .Distinct()
                     .OrderBy(uid => uid))
        {
            UidList.Add(uid);
        }

        SelectUid = UidList.Count > 0 ? UidList[0] : null;
        _suppressUidRebuild = false;
        OnPropertyChanged(nameof(HasUids));
    }


    private void Rebuild()
    {
        Pools.Clear();
        ItemStats.Clear();

        var records = SelectUid is long uid && uid > 0
            ? _allRecords.Where(r => r.Uid == uid).ToList()
            : _allRecords.ToList();

        var stats = GachaService.ComputeStatistics(records);
        var byType = records
            .GroupBy(r => r.GachaType)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<GachaRecord>)g.ToList());

        foreach (var statistics in stats)
        {
            if (_selectedPools.Count > 0 && !_selectedPools.Contains(statistics.GachaType))
            {
                continue;
            }

            Pools.Add(new GachaPoolCardViewModel(
                statistics,
                byType[statistics.GachaType],
                PoolText(statistics.GachaType),
                Strings["Gacha.Pity"]));
        }

        // Starward GachaLogService.cs:251-256：按 ItemId 分组 → 稀有度降序、次数降序、时间降序。
        // 这里没有 ItemId，按名称分组。
        foreach (var group in records
                     .GroupBy(r => r.Name)
                     .Select(g => new
                     {
                         Name = g.Key,
                         RankType = g.Max(r => r.RankType),
                         Count = g.Count(),
                         Time = g.Max(r => r.Time),
                     })
                     .OrderByDescending(x => x.RankType)
                     .ThenByDescending(x => x.Count)
                     .ThenByDescending(x => x.Time))
        {
            ItemStats.Add(new GachaItemStatsViewModel(group.Name, group.RankType, group.Count));
        }

        SyncBannerOptions(stats);

        OnPropertyChanged(nameof(HasPools));
        OnPropertyChanged(nameof(HasItemStats));
        OnPropertyChanged(nameof(HasRecords));
        OnPropertyChanged(nameof(ShowEmptyState));
    }


    /// <summary>
    /// 卡池类型 → 显示名。Starward 用 <c>IGachaType.ToLocalization()</c>；
    /// 这里从 AppStrings 取同义文案，查不到时退回 <see cref="GachaService.GetPoolName"/>。
    /// </summary>
    private string PoolText(int gachaType)
    {
        var key = (_gameKind == "zzz" ? "Gacha.Pool.Zzz." : "Gacha.Pool.Genshin.") + gachaType;
        var text = Strings[key];
        return text == key ? GachaService.GetPoolName(_gameKind, gachaType) : text;
    }


    private void SyncBannerOptions(IReadOnlyList<GachaPoolStatistics> stats)
    {
        var types = stats.Select(s => s.GachaType).ToList();
        var sameSet = BannerOptions.Count == types.Count
                      && BannerOptions.Select(b => b.Value).SequenceEqual(types);
        if (sameSet)
        {
            return;
        }

        _suppressBannerCallback = true;
        BannerOptions.Clear();
        foreach (var statistics in stats)
        {
            BannerOptions.Add(new GachaBannerViewModel(
                statistics.GachaType,
                PoolText(statistics.GachaType),
                OnBannerSelectionChanged)
            {
                IsSelected = _selectedPools.Contains(statistics.GachaType),
            });
        }

        _suppressBannerCallback = false;
        OnPropertyChanged(nameof(HasBannerOptions));
    }


    private void OnBannerSelectionChanged(GachaBannerViewModel banner)
    {
        if (_suppressBannerCallback)
        {
            return;
        }

        if (banner.IsSelected)
        {
            _selectedPools.Add(banner.Value);
        }
        else
        {
            _selectedPools.Remove(banner.Value);
        }

        Rebuild();
    }


    public override void Dispose()
    {
        _disposed = true;
        _allRecords = [];
        Pools.Clear();
        ItemStats.Clear();
        UidList.Clear();
        BannerOptions.Clear();
    }
}
