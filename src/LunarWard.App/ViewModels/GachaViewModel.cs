using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LunarWard.App.Services;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels;

/// <summary>
/// 抽卡记录窗口：读取链接、拉取记录、按卡池统计。
/// </summary>
/// <remarks>
/// 视图只做展示，统计全部来自 <see cref="GachaService.ComputeStatistics"/>（纯函数，有单测）。
/// </remarks>
public sealed partial class GachaViewModel : ObservableObject
{
    private readonly AppServices _services;

    private readonly GachaService _gacha;

    private readonly string _gameDirectory;

    private readonly string _gameBiz;

    private readonly string _gameKind;


    public GachaViewModel(AppServices services, ProfileItemViewModel profile, GachaService? gacha = null)
    {
        _services = services;
        _gacha = gacha ?? new GachaService();
        _gameDirectory = ProfileConfig.ExpandPath(profile.Profile.Path);
        _gameKind = profile.Profile.Game;

        var definition = Games.GameCatalog.Get(
            profile.Profile.Game,
            string.Equals(profile.Profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
                ? Games.GameEdition.Global
                : Games.GameEdition.China);
        _gameBiz = definition?.GameBiz ?? "hk4e_cn";

        Pools = new ObservableCollection<GachaPoolViewModel>();
        Records = new ObservableCollection<GachaRecordViewModel>();

        // 打开窗口时先尝试自动定位链接（不自动拉取，避免刚开页面就打网络）
        var url = GachaService.FindGachaUrl(_gameDirectory, new LunarWard.Core.GameBiz(_gameBiz), out var error);
        StatusText = url is null ? (error ?? "") : "已找到抽卡链接，点『读取记录』开始";
        GachaUrl = url ?? "";
    }


    public ObservableCollection<GachaPoolViewModel> Pools { get; }

    public ObservableCollection<GachaRecordViewModel> Records { get; }

    public string Title => $"{_gameKind} · 抽卡记录";


    [ObservableProperty]
    private string gachaUrl = "";


    [ObservableProperty]
    private string statusText = "";


    [ObservableProperty]
    private string? errorText;


    [ObservableProperty]
    private bool isBusy;


    [ObservableProperty]
    private string summaryText = "";


    /// <summary>只显示 5 星。</summary>
    [ObservableProperty]
    private bool fiveStarsOnly;


    partial void OnFiveStarsOnlyChanged(bool value) => ApplyFilter();


    private IReadOnlyList<GachaRecord> _allRecords = [];


    [RelayCommand]
    private void Relocate()
    {
        var url = GachaService.FindGachaUrl(_gameDirectory, new LunarWard.Core.GameBiz(_gameBiz), out var error);
        if (url is null)
        {
            ErrorText = error;
            return;
        }

        GachaUrl = url;
        StatusText = "已重新定位抽卡链接";
        ErrorText = null;
    }


    [RelayCommand]
    private async Task FetchAsync()
    {
        if (string.IsNullOrWhiteSpace(GachaUrl))
        {
            ErrorText = "还没有抽卡链接。先点『重新定位』，或在游戏里打开一次祈愿页面。";
            return;
        }

        IsBusy = true;
        ErrorText = null;
        var pages = new Dictionary<string, int>(StringComparer.Ordinal);
        var progress = new Progress<(string Pool, int Page)>(p =>
        {
            pages[p.Pool] = p.Page;
            StatusText = string.Join("  ", pages.Select(kv => $"{kv.Key} 第 {kv.Value} 页"));
        });

        try
        {
            _allRecords = await _gacha.FetchAsync(_gameKind, GachaUrl, progress);
            Rebuild();
            StatusText = $"共 {_allRecords.Count} 条记录";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
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
            ErrorText = "没有可导出的记录";
            return;
        }

        try
        {
            var dir = Path.Join(_services.Paths.DataHome, "gacha");
            Directory.CreateDirectory(dir);
            var path = Path.Join(dir, $"{_gameKind}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            Platform.AtomicFile.WriteAllText(path,
                System.Text.Json.JsonSerializer.Serialize(_allRecords,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    }));
            StatusText = $"已导出 {path}";
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }


    private void Rebuild()
    {
        Pools.Clear();
        foreach (var stat in GachaService.ComputeStatistics(_allRecords))
        {
            Pools.Add(new GachaPoolViewModel(stat));
        }

        ApplyFilter();

        var total = _allRecords.Count;
        var fiveStars = _allRecords.Count(r => r.IsFiveStar);
        SummaryText = total == 0
            ? "暂无记录"
            : $"共 {total} 抽 · 5 星 {fiveStars} 个 · 5 星率 {(double)fiveStars / total:P2}";
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
    }
}


/// <summary>一个卡池的统计卡片。</summary>
public sealed class GachaPoolViewModel
{
    public GachaPoolViewModel(GachaPoolStatistics statistics)
    {
        Statistics = statistics;
    }


    public GachaPoolStatistics Statistics { get; }

    public string Name => Statistics.PoolName;

    public string Pulls => $"共 {Statistics.TotalPulls} 抽";

    public string FiveStars => $"5 星 {Statistics.FiveStarCount} 个";

    public string Pity => $"当前保底进度 {Statistics.CurrentPity}";

    public string Average => Statistics.FiveStarCount == 0
        ? "平均出货 —"
        : $"平均 {Statistics.AveragePity:0.0} 抽出货";

    public string PityHistory => Statistics.FiveStarPities.Count == 0
        ? ""
        : "历史：" + string.Join(" / ", Statistics.FiveStarPities);
}


/// <summary>一条记录的展示。</summary>
public sealed class GachaRecordViewModel
{
    public GachaRecordViewModel(GachaRecord record)
    {
        Record = record;
    }


    public GachaRecord Record { get; }

    public string Name => Record.Name;

    public string ItemType => Record.ItemType;

    public string Stars => new string('★', Math.Clamp(Record.RankType, 0, 5));

    public string Pool => Record.GachaTypeName;

    public string Time => Record.Time.ToString("yyyy-MM-dd HH:mm:ss");

    public bool IsFiveStar => Record.IsFiveStar;
}
