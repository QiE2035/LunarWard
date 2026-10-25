using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.ViewModels;
using LunarWard.App.Views.Starward;
using LunarWard.Games;
using LunarWard.Launch;
using LunarWard.Launch.Database;
using LunarWard.Launch.Uigf4;
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
/// 存储与 Starward 一致：SQLite（<c>&lt;DataHome&gt;/LunarWardDatabase.db</c>，表结构照抄
/// <c>StarwardDatabase.db</c>），更新记录后写库、删除时间段直接删行、打开页面先从库里读。
/// 已知差异：物品图标要等 wiki 信息表同步（<c>UpdateWikiDataAsync</c>，首次打开页面后台拉取），
/// 没同步前显示 28×28 底色占位；<c>up!</c> 由 <c>GachaNoUp</c> 表重算，行为与上游一致。
/// </para>
/// </remarks>
public sealed partial class GachaPageViewModel : StarwardPageViewModel
{
    private readonly GachaService _gacha = new();

    /// <summary>本地抽卡数据库（UIGF v4.2 窗口的数据源，表结构与 Starward 一致）。</summary>
    private readonly GachaLogStore _archiveStore;

    private readonly string _gameDirectory;

    private readonly string _gameBiz;

    private readonly string _gameKind;

    private IReadOnlyList<GachaRecord> _allRecords = [];

    /// <summary>物品图标映射（wiki 信息表：ItemId → 图标 URL），由 <see cref="UpdateWikiDataAsync"/> 刷新。</summary>
    private IReadOnlyDictionary<int, string> _icons = new Dictionary<int, string>();

    private bool _wikiUpdateStarted;

    /// <summary>「隐藏卡池」勾选项（空集合 = 显示全部，语义同 Starward GachaLogPage.xaml.cs:381-384）。</summary>
    private readonly HashSet<int> _selectedPools = [];

    private bool _suppressUidRebuild;

    private bool _suppressBannerCallback;

    private bool _disposed;


    public GachaPageViewModel(PageContext context, string? gameKindOverride = null, GachaLogStore? store = null) : base(context)
    {
        _gameDirectory = ProfileConfig.ExpandPath(Profile.Profile.Path);
        // 自检截图可以注入临时库，避免样例记录写进用户数据
        _archiveStore = store ?? GachaLogStore.OpenDefault(context.Services.Paths.DataHome);
        // 自检（--gacha-seed zzz）需要在没装绝区零的机器上也算 ZZZ 版式
        _gameKind = gameKindOverride ?? Profile.Profile.Game;

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

        // 上游打开页面就从数据库读已保存的记录（不必先点「更新记录」）
        _allRecords = LoadRecordsFromDatabase();
        if (_allRecords.Count > 0)
        {
            RebuildUidList();
            Rebuild();
            StatusText = string.Format(Strings["Gacha.LoadedFromDatabase"], _allRecords.Count);
            _ = UpdateWikiDataAsync();
        }
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

    /// <summary>当前记录快照（删除对话框用来算范围内条数）。</summary>
    public IReadOnlyList<GachaRecord> RecordsSnapshot => _allRecords;

    /// <summary>绝区零用 <c>ZZZGachaStatsCard</c>（Starward <c>IsZZZGachaStatsCardVisible</c>）。</summary>
    public bool IsZzz => _gameKind == "zzz";

    /// <summary>统计卡：绝区零用 S/A/B 版式，其它游戏用 5★/4★/3★ 版式。</summary>
    public bool ShowZzzStatsCard => IsZzz && HasPools;

    /// <summary>非绝区零的统计卡。</summary>
    public bool ShowGenericStatsCard => !IsZzz && HasPools;

    /// <summary>没有任何记录 → 整页居中空态（Starward StackPanel_Emoji）。</summary>
    public bool ShowEmptyState => !HasRecords;


    // ── 设置 Flyout 文案 / 占位状态 ────────────────────────────────────

    /// <summary>旧版导出格式名（Starward GachaLogPage.xaml.cs:59-70）：原神 UIGF v3.0，绝区零 JSON。</summary>
    public string LegacyFormatName => GachaExport.LegacyFormatName(_gameKind);

    /// <summary>「从 UIGF v3.0 导入」按钮文字（Starward <c>LegacyImportText</c>）。</summary>
    public string LegacyImportText => string.Format(Strings["Gacha.ImportFromLegacy"], LegacyFormatName);

    /// <summary>抽卡语言（Starward <c>GachaLanguage</c>，默认跟随系统语言）。</summary>
    /// <remarks>
    /// 上游用 <c>LanguageUtil.FilterLanguage(CultureInfo.CurrentUICulture.Name)</c> 初始化，
    /// 用户可在设置 Flyout 里改成别的语言，再点「应用」按该语言重命名本地记录。
    /// </remarks>
    [ObservableProperty]
    private string _gachaLanguage = LunarWard.Core.LanguageUtil.FilterLanguage(
        System.Globalization.CultureInfo.CurrentUICulture.Name);

    /// <summary>
    /// UIGF v4 导入导出是否可用。
    /// </summary>
    /// <remarks>UIGF v4.2 窗口已移植（导出/导入都接在 SQLite 存档上）。</remarks>
    public bool IsUigfAvailable => true;

    public string UigfHint => Strings["Gacha.UigfHint"];

    /// <summary>页面共用的存档（UIGF 窗口传同一个实例，自检时两边看到同一份数据）。</summary>
    public GachaLogStore ArchiveStore => _archiveStore;


    /// <summary>当前页面绑定的游戏 kind（<c>genshin</c> / <c>starrail</c> / <c>zzz</c>）。</summary>
    public string GameKind => _gameKind;

    /// <summary>UIGF v3.0 / JSON 旧格式导入是否可用（Starward <c>ImportGachaLogCommand</c>）。</summary>
    public bool IsImportAvailable => true;

    public string ImportHint => Strings["Gacha.ImportHint"];

    /// <summary>抽卡语言切换是否可用（wiki 名称表 + 记录改名都已接好）。</summary>
    public bool IsLanguageChangeAvailable => true;

    public string LanguageHint => Strings["Gacha.LanguageHint"];


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

            // 上游：先 INSERT OR REPLACE 进数据库，再从库里读出（删除过的区间不会回来）
            _archiveStore.Merge(_gameKind, records, GachaLanguage);
            // 上游 GetGachaLogAsync 成功后把链接按 (GameBiz, Uid) 存进 GachaLogUrl 表
            long uid = records.FirstOrDefault(r => r.Uid > 0)?.Uid ?? 0;
            if (uid > 0 && !string.IsNullOrWhiteSpace(GachaUrl))
            {
                _archiveStore.SaveGachaLogUrl(_gameBiz, uid, GachaUrl);
            }
            _allRecords = LoadRecordsFromDatabase();
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


    /// <summary>自检：把本地存档按 UIGF v4.2 写到指定路径（避开文件对话框）。</summary>
    public void ExportUigfTo(string path)
    {
        try
        {
            var service = new Uigf4Service(
                _archiveStore,
                "self-test",
                CaptureMode.Enabled
                    ? CaptureMode.CreateBeyondStore()
                    : Launch.Database.GenshinBeyondGachaStore.OpenDefault(AppServices.Current.Paths.DataHome));
            string json = service.BuildJson(service.GetArchives());
            Platform.AtomicFile.WriteAllText(path, json);
            ErrorText = null;
            StatusText = string.Format(Strings["Gacha.Uigf.Exported"], path);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    /// <summary>
    /// 按 <see cref="GachaLanguage"/> 重新命名全部本地记录（上游 <c>ChangeGachaItemNameCommand</c>）。
    /// </summary>
    /// <remarks>
    /// 名称表来自抽卡 wiki（<see cref="GachaService.ChangeItemNamesAsync"/>），改完 UPDATE 库里的
    /// <c>Name</c> / <c>Lang</c> 两列（上游 <c>ChangeGachaItemNameAsync</c>）；
    /// 网络失败只落错误条，不动现有记录。
    /// </remarks>
    [RelayCommand]
    private async Task ChangeGachaLanguageAsync()
    {
        if (IsBusy || _allRecords.Count == 0)
        {
            if (_allRecords.Count == 0)
            {
                ErrorText = Strings["Gacha.NoRecordsToRename"];
            }
            return;
        }

        IsBusy = true;
        ErrorText = null;
        StatusText = Strings["Gacha.LanguageApplying"];

        try
        {
            var (records, renamed) = await _gacha.ChangeItemNamesAsync(
                _gameKind,
                new LunarWard.Core.GameBiz(_gameBiz),
                GachaLanguage,
                _allRecords).ConfigureAwait(true);
            if (_disposed)
            {
                return;
            }

            // 上游改名是 UPDATE Name, Lang：只改库里的名字，不重写其它列
            _archiveStore.Rename(_gameKind, GachaLanguage, records);
            await RefreshWikiDataAsync().ConfigureAwait(true);
            _allRecords = LoadRecordsFromDatabase();
            Rebuild();
            StatusText = string.Format(Strings["Gacha.LanguageChanged"], GachaLanguage, renamed);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            Log.Warn($"抽卡记录改名失败：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }


    /// <summary>页面订阅它来弹「保存文件」对话框（<c>StorageProvider</c> 只能在视图层拿）。</summary>
    public event Action? ExportJsonRequested;

    /// <summary>页面订阅它来弹「保存文件」对话框（Excel 导出）。</summary>
    public event Action? ExportExcelRequested;

    /// <summary>页面订阅它来打开 UIGF v4.2 窗口。</summary>
    public event Action? UigfRequested;

    /// <summary>页面订阅它来弹「打开文件」对话框（旧格式导入）。</summary>
    public event Action? ImportLegacyRequested;

    /// <summary>旧格式导入按钮：交给视图层选文件（<c>StorageProvider</c> 在视图层）。</summary>
    [RelayCommand]
    private void ImportLegacy() => ImportLegacyRequested?.Invoke();


    /// <summary>
    /// 读入旧格式文件（UIGF v3.0 / JSON），按 <c>Id</c> 合并进本地记录并落盘存档。
    /// </summary>
    /// <remarks>
    /// 上游导入是「写数据库 + 刷新」；这里同样写进 SQLite 再从库里重读，失败只落错误条。
    /// </remarks>
    /// <returns>导入的记录条数；失败返回 -1 并落错误条。</returns>
    public int ImportLegacyFrom(string path)
    {
        try
        {
            var imported = GachaLegacyImport.ParseFile(path, _gameKind);
            // 上游导入也是写库 + 刷新：这里同样落库后从库里重读
            _archiveStore.Merge(_gameKind, imported, GachaLanguage);
            _allRecords = LoadRecordsFromDatabase();
            RebuildUidList();
            Rebuild();
            ErrorText = null;
            StatusText = string.Format(Strings["Gacha.ImportDone"], imported.Count, _allRecords.Count);
            return imported.Count;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            Log.Warn($"导入旧格式抽卡记录失败：{ex.Message}");
            return -1;
        }
    }


    [RelayCommand]
    private void OpenUigf() => UigfRequested?.Invoke();


    /// <summary>Excel 模板是否随程序分发（<c>Assets/Template/GachaLog.xlsx</c>）。</summary>
    public bool IsExcelAvailable => GachaExcelExport.IsAvailable(_gameKind);


    [RelayCommand]
    private void ExportExcel()
    {
        if (_allRecords.Count == 0)
        {
            ErrorText = Strings["Gacha.NoExport"];
            return;
        }

        ExportExcelRequested?.Invoke();
    }


    /// <summary>Excel 导出建议文件名（<c>UIGF_UID_时间.xlsx</c> 的等价物：<c>GachaLog-Uid-时间.xlsx</c>）。</summary>
    public string SuggestedExcelFileName
    {
        get
        {
            long uid = SelectUid ?? _allRecords.FirstOrDefault()?.Uid ?? 0;
            return $"GachaLog-{uid}-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
        }
    }


    /// <summary>
    /// 按 MiniExcel 模板写出 Excel（Starward <c>ExportAsExcelAsync</c> 的等价物）。
    /// </summary>
    public async Task ExportExcelToAsync(string path)
    {
        try
        {
            long uid = SelectUid ?? _allRecords.FirstOrDefault()?.Uid ?? 0;
            await GachaExcelExport.ExportAsync(path, _gameKind, uid, _allRecords);
            ErrorText = null;
            StatusText = string.Format(Strings["Gacha.Exported"], path);
            Log.Info($"导出抽卡记录到 {path}（Excel）");
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            Log.Warn($"导出 Excel 失败：{ex.Message}");
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

        ExportJsonRequested?.Invoke();
    }


    /// <summary>导出建议文件名（UIGF v3.0-UID-时间.json / SRGF v1.0-... / JSON-...）。</summary>
    public string SuggestedExportFileName
    {
        get
        {
            long uid = SelectUid ?? _allRecords.FirstOrDefault()?.Uid ?? 0;
            return GachaExport.FileName(_gameKind, uid, DateTime.Now);
        }
    }


    /// <summary>
    /// 写入 UIGF v3.0 / SRGF v1.0（Starward 三个 <c>ExportAsJsonAsync</c> 的信封）。
    /// </summary>
    public void ExportJsonTo(string path)
    {
        try
        {
            long uid = SelectUid ?? _allRecords.FirstOrDefault()?.Uid ?? 0;
            string version = typeof(GachaPageViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
            string json = GachaExport.BuildJson(_allRecords, _gameKind, uid, GachaLanguage, version);
            Platform.AtomicFile.WriteAllText(path, json);
            ErrorText = null;
            StatusText = string.Format(Strings["Gacha.Exported"], path);
            Log.Info($"导出抽卡记录到 {path}（{LegacyFormatName}）");
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


    /// <summary>删除记录 → 删掉当前 UID 的全部记录（上游 <c>DeleteUid</c>）。</summary>
    [RelayCommand]
    private void ClearRecords()
    {
        if (SelectUid is long uid && uid > 0)
        {
            _archiveStore.DeleteUid(_gameKind, uid);
            _allRecords = LoadRecordsFromDatabase();
        }
        else
        {
            _allRecords = [];
        }
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


    /// <summary>
    /// 后台同步 wiki 物品信息（对应上游 <c>GachaLogPage.xaml.cs:220-232 UpdateWikiDataAsync</c>）。
    /// </summary>
    /// <remarks>
    /// 只负责「图标 URL + 老记录的 ItemId 回填」：拉 <c>GachaInfo</c> 接口 → 写
    /// <c>Genshin/StarRail/ZZZGachaInfo</c> 表 → 用名字回填 <c>ItemId=0</c> 的记录 → 重读重绘。
    /// 网络失败静默（图标保持占位），不弹错误条 —— 上游同样只写日志。
    /// </remarks>
    private async Task UpdateWikiDataAsync()
    {
        if (_wikiUpdateStarted)
        {
            return;
        }

        _wikiUpdateStarted = true;
        await RefreshWikiDataAsync().ConfigureAwait(true);
    }


    private async Task RefreshWikiDataAsync()
    {
        try
        {
            _icons = _archiveStore.GetIconMap(_gameKind);
            if (_icons.Count > 0)
            {
                Rebuild();
                return;
            }

            string lang = string.IsNullOrWhiteSpace(GachaLanguage)
                ? System.Globalization.CultureInfo.CurrentUICulture.Name
                : GachaLanguage;
            IReadOnlyList<GachaInfoItem> items = await _gacha
                .FetchGachaInfoAsync(_gameKind, new LunarWard.Core.GameBiz(_gameBiz), lang)
                .ConfigureAwait(true);
            if (_disposed || items.Count == 0)
            {
                return;
            }

            _archiveStore.UpsertGachaInfo(_gameKind, items);
            _archiveStore.UpdateItemIds(_gameKind);
            _allRecords = LoadRecordsFromDatabase();
            _icons = _archiveStore.GetIconMap(_gameKind);
            RebuildUidList();
            Rebuild();
        }
        catch (Exception)
        {
            // 与上游一致：wiki 拉取失败只影响图标/名字，不打扰用户。
        }
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
                Strings["Gacha.Pity"],
                zzz: _gameKind == "zzz",
                icons: _icons));
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
        OnPropertyChanged(nameof(ShowZzzStatsCard));
        OnPropertyChanged(nameof(ShowGenericStatsCard));
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


    /// <summary>
    /// 页面订阅它来弹「删除指定时间段」对话框（Starward 在页面代码里 ShowAsync）。
    /// </summary>
    public event Action? DeleteByTimeRequested;


    [RelayCommand]
    private void DeleteByTime() => DeleteByTimeRequested?.Invoke();


    /// <summary>
    /// 删除某个时间段后立刻从内存里摘掉这些记录（对话框确认后由页面调用）。
    /// </summary>
    public void ApplyDeletedRange(long uid, DateTime begin, DateTime end)
    {
        // 上游 DeleteGachaLogByTime：DELETE FROM 表 WHERE Uid=@uid AND Time BETWEEN
        int before = _allRecords.Count;
        _archiveStore.DeleteRange(_gameKind, uid, begin, end);
        _allRecords = LoadRecordsFromDatabase();
        int removed = before - _allRecords.Count;
        RebuildUidList();
        Rebuild();
        StatusText = string.Format(Strings["Gacha.DeletedCount"], removed);
        Log.Info($"删除抽卡记录 {removed} 条（uid {uid}，{begin:yyyy-MM-dd HH:mm} - {end:yyyy-MM-dd HH:mm}）");
    }


    /// <summary>
    /// 自检用：直接灌入样例记录（不碰网络、不写用户数据）。
    /// </summary>
    public void LoadRecordsForCapture(IReadOnlyList<Launch.GachaRecord> records)
    {
        _allRecords = Launch.GachaService.ApplyUpFlags(_gameKind, records);
        // 自检样例也落盘，UIGF 窗口才有存档可导
        _archiveStore.Merge(_gameKind, _allRecords, GachaLanguage);
        // 样例记录没有 ItemId：先同步 wiki 再按名字回填（真实抓取时接口自带 ItemId，不会被覆盖）
        _archiveStore.UpdateItemIds(_gameKind);
        _allRecords = LoadRecordsFromDatabase();
        RebuildUidList();
        Rebuild();
        StatusText = string.Format(Strings["Gacha.FetchDone"], _allRecords.Count);
        // 截图要看到图标：后台同步 wiki 信息表（网络失败就保持占位块）
        _ = UpdateWikiDataAsync();
    }


    /// <summary>
    /// 从库里读记录并补算 <c>IsUp</c>：库里没有该列，<c>up!</c> 由 <see cref="Launch.GachaService.ApplyUpFlags"/>
    /// 按上游 <c>GachaNoUp</c> 的卡池时间表重算（与 Starward 的读取路径一致）。
    /// </summary>
    private IReadOnlyList<Launch.GachaRecord> LoadRecordsFromDatabase()
        => Launch.GachaService.ApplyUpFlags(_gameKind, _archiveStore.GetAll(_gameKind));


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
