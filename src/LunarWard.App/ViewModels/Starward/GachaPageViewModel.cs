using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.ViewModels;
using LunarWard.App.Views.Starward;
using LunarWard.Games;
using LunarWard.Launch;
using LunarWard.Platform;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 抽卡记录页（复刻 Starward <c>GachaLogPage</c>）：顶部工具条 + 卡池统计卡片流 + 记录列表。
/// </summary>
/// <remarks>
/// <para>
/// 拉取、链接定位、统计全部复用既有实现：<see cref="GachaService.FindGachaUrl"/> /
/// <see cref="GachaService.FetchAsync"/> / <see cref="GachaService.ComputeStatistics"/>。
/// 页面 VM <b>不重算</b>任何统计，只把 <see cref="GachaService.ComputeStatistics"/> 的结果
/// （<see cref="GachaPoolViewModel"/>）包成可本地化的卡片。
/// </para>
/// <para>
/// 版式见 <c>docs/07-UI-REPLICA.md</c> §5；Starward 的统计卡对应
/// <c>Features/Gacha/GachaStatsCard.xaml</c>（圆角 8、卡池名 + 抽数 + 5★ 统计 + 出金列表）。
/// </para>
/// </remarks>
public sealed partial class GachaPageViewModel : StarwardPageViewModel
{
    private readonly GachaService _gacha = new();

    private readonly string _gameDirectory;

    private readonly string _gameBiz;

    private readonly string _gameKind;

    private IReadOnlyList<GachaRecord> _allRecords = [];

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
        Records = [];

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


    // ── 状态 ──────────────────────────────────────────────────────────

    /// <summary>抽卡链接（来自游戏网页缓存，可手工粘贴）。</summary>
    [ObservableProperty]
    private string _gachaUrl = "";


    /// <summary>次要色状态行（读取进度 / 已定位 / 导出结果）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusText = "";

    /// <summary>状态行是否有内容（空时不占高度）。</summary>
    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);


    [ObservableProperty]
    private string? _errorText;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));


    [ObservableProperty]
    private bool _isBusy;


    [ObservableProperty]
    private string _summaryText = "";


    /// <summary>只显示 5★ 记录。</summary>
    [ObservableProperty]
    private bool _fiveStarsOnly;

    partial void OnFiveStarsOnlyChanged(bool value) => ApplyFilter();


    // ── 数据 ──────────────────────────────────────────────────────────

    /// <summary>卡池统计卡（数据来自 <see cref="GachaPoolViewModel"/>，不重算）。</summary>
    public ObservableCollection<GachaPoolCardViewModel> Pools { get; }

    /// <summary>记录行（跨游戏统一形态）。</summary>
    public ObservableCollection<GachaRecordViewModel> Records { get; }

    public bool HasPools => Pools.Count > 0;

    public bool HasRecords => Records.Count > 0;

    /// <summary>是否已经拉到任何记录（与"当前过滤后是否为空"区分）。</summary>
    public bool HasData => _allRecords.Count > 0;

    /// <summary>一条记录都没有 → 整块空态。</summary>
    public bool ShowEmptyState => !HasData;

    /// <summary>有记录，但被「只看 5 星」过滤空了 → 列表区内的小提示，而不是整页空态。</summary>
    public bool ShowFilteredEmpty => HasData && !HasRecords;

    /// <summary>
    /// 空态副标题：固定讲"怎么把记录弄出来"。
    /// </summary>
    /// <remarks>
    /// 具体失败原因（没设游戏目录、缓存里没链接）由上面的错误条负责，
    /// 这里不重复说同一件事，否则两行文字打架。
    /// </remarks>
    public string EmptyDetail => Strings["Gacha.EmptyDetail"];

    public string RecordCountText => string.Format(Strings["Gacha.RecordCount"], Records.Count);

    /// <summary>游戏标题（Starward 页头左侧的 20px 文本）。</summary>
    public string GameName => Profile.DisplayName;


    // ── UIGF 入口 ────────────────────────────────────────────────────

    /// <summary>
    /// UIGF v4 导入导出是否可用。
    /// </summary>
    /// <remarks>
    /// <c>LunarWard.Core</c> 目前还没有 UIGF 读写实现（见 <c>docs/05-ROADMAP.md</c>），
    /// 按契约把入口做成禁用并注明"待接"，不伪造功能。
    /// </remarks>
    public bool IsUigfAvailable => false;

    public string UigfHint => Strings["Gacha.UigfPending"];


    // ── 主操作 ────────────────────────────────────────────────────────

    /// <summary>有链接才能读取记录；没有时强调色给「重新定位」。</summary>
    public bool IsFetchPrimary => !string.IsNullOrWhiteSpace(GachaUrl) && !IsBusy;

    /// <summary>没有链接时的主操作是「重新定位」。</summary>
    public bool IsRelocatePrimary => !IsFetchPrimary;

    partial void OnGachaUrlChanged(string value) => RaisePrimaryChanged();

    partial void OnIsBusyChanged(bool value) => RaisePrimaryChanged();

    private void RaisePrimaryChanged()
    {
        OnPropertyChanged(nameof(IsFetchPrimary));
        OnPropertyChanged(nameof(IsRelocatePrimary));
    }


    // ── 命令 ──────────────────────────────────────────────────────────

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


    // ── 内部 ──────────────────────────────────────────────────────────

    private void Rebuild()
    {
        Pools.Clear();
        foreach (var statistics in GachaService.ComputeStatistics(_allRecords))
        {
            Pools.Add(new GachaPoolCardViewModel(new GachaPoolViewModel(statistics)));
        }

        ApplyFilter();
        UpdateSummary();

        OnPropertyChanged(nameof(HasPools));
    }


    private void ApplyFilter()
    {
        Records.Clear();
        var source = FiveStarsOnly
            ? GachaService.GetFiveStars(_allRecords, max: 200)
            : _allRecords.OrderByDescending(r => r.Time).Take(500);

        foreach (var record in source)
        {
            Records.Add(new GachaRecordViewModel(record));
        }

        OnPropertyChanged(nameof(HasRecords));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowFilteredEmpty));
        OnPropertyChanged(nameof(EmptyDetail));
        OnPropertyChanged(nameof(RecordCountText));
    }


    private void UpdateSummary()
    {
        if (_allRecords.Count == 0)
        {
            SummaryText = Strings["Gacha.SummaryEmpty"];
            return;
        }

        var total = _allRecords.Count;
        var fiveStars = _allRecords.Count(r => r.IsFiveStar);
        SummaryText = string.Format(Strings["Gacha.Summary"], total, fiveStars, $"{(double)fiveStars / total:P1}");
    }


    public override void Dispose()
    {
        _disposed = true;
        _allRecords = [];
        Pools.Clear();
        Records.Clear();
    }
}
