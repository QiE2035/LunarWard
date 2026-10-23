using LunarWard.App.Localization;
using LunarWard.App.ViewModels;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 一张卡池统计卡（Starward <c>GachaStatsCard</c> 的等价物）。
/// </summary>
/// <remarks>
/// 数值全部来自既有的 <see cref="GachaPoolViewModel"/> / <see cref="GachaPoolStatistics"/>，
/// 这里只做"取已算好的字段 + 按当前语言排版"，不做任何统计重算。
/// </remarks>
public sealed class GachaPoolCardViewModel
{
    private readonly GachaPoolViewModel _pool;


    public GachaPoolCardViewModel(GachaPoolViewModel pool)
    {
        _pool = pool;
    }


    /// <summary>被包装的既有 VM（统计的唯一来源）。</summary>
    public GachaPoolViewModel Pool => _pool;

    public GachaPoolStatistics Statistics => _pool.Statistics;

    /// <summary>卡池名（Starward 卡片首行，16px 次要色）。</summary>
    public string Name => _pool.Name;

    public string PullsText => string.Format(Strings["Gacha.Card.Pulls"], Statistics.TotalPulls);

    public string FiveStarsText => string.Format(Strings["Gacha.Card.FiveStars"], Statistics.FiveStarCount);

    /// <summary>5★ 出率（0 抽时 Starward 显示 —）。</summary>
    public string FiveStarRateText => Statistics.TotalPulls == 0
        ? "—"
        : $"{(double)Statistics.FiveStarCount / Statistics.TotalPulls:P1}";

    /// <summary>距上一发 5★ 的抽数（保底进度，卡片里的强调色一行）。</summary>
    public string PityText => string.Format(Strings["Gacha.Card.Pity"], Statistics.CurrentPity);

    public string AverageText => Statistics.FiveStarCount == 0
        ? Strings["Gacha.Card.AverageNone"]
        : string.Format(Strings["Gacha.Card.Average"], Statistics.AveragePity);

    /// <summary>出金历史（每发 5★ 用了多少抽，从旧到新）。</summary>
    public string PityHistoryText => Statistics.FiveStarPities.Count == 0
        ? Strings["Gacha.Card.NoFiveStar"]
        : string.Join(" · ", Statistics.FiveStarPities);

    public bool HasPityHistory => Statistics.FiveStarPities.Count > 0;


    private static AppStrings Strings => AppStrings.Instance;
}
