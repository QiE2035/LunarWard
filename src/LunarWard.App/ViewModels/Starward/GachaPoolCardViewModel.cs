using CommunityToolkit.Mvvm.ComponentModel;
using LunarWard.App.Localization;
using LunarWard.Launch;

namespace LunarWard.App.ViewModels.Starward;

/// <summary>
/// 一张卡池统计卡（Starward <c>Features/Gacha/GachaStatsCard.xaml</c> 的数据侧等价物）。
/// </summary>
/// <remarks>
/// <para>
/// 字段与 Starward <c>Features/Gacha/GachaTypeStats.cs</c> 一一对应：
/// <c>GachaType / GachaTypeText / Count / StartTime / EndTime / Count_5..3 /
/// Ratio_5..3 / Average_5 / List_5 / List_4</c>。
/// 计算口径照 <c>GachaLogService.GetGachaTypeStats</c>（GachaLogService.cs:180-250）：
/// 记录按时间正序排列，逐条累计保底，5★/4★ 列表倒序（最新在前），首行插入「已垫」占位行。
/// </para>
/// <para>
/// 统计值来自 <see cref="GachaPoolStatistics"/>（<c>GachaService.ComputeStatistics</c> 的纯函数结果），
/// 列表与时间区间来自同一批 <see cref="GachaRecord"/>；本类只做整形，不重算抽样逻辑。
/// </para>
/// </remarks>
public sealed partial class GachaPoolCardViewModel : ObservableObject
{
    public GachaPoolCardViewModel(
        GachaPoolStatistics statistics,
        IReadOnlyList<GachaRecord> records,
        string poolText,
        string pityText)
    {
        GachaType = statistics.GachaType;
        GachaTypeText = poolText;
        Count = ResolveCount(statistics, records);

        // Starward GachaLogService.cs:189-191：StartTime = list.First().Time，EndTime = list.Last().Time
        var ordered = records.OrderBy(r => r.Time).ThenBy(r => r.Id).ToList();
        StartTime = ordered[0].Time;
        EndTime = ordered[^1].Time;
        TimeRangeText = $"{StartTime:yyyy/MM/dd HH:mm:ss} - {EndTime:yyyy/MM/dd HH:mm:ss}";

        // Starward GachaLogService.cs:184-196 + 211-220：一次遍历同时算 3/4/5 星数量和每行保底
        var list5 = new List<GachaStatsItemViewModel>();
        var list4 = new List<GachaStatsItemViewModel>();
        var count5 = 0;
        var count4 = 0;
        var count3 = 0;
        var pity5 = 0;
        var pity4 = 0;

        foreach (var record in ordered)
        {
            pity5++;
            pity4++;

            switch (record.RankType)
            {
                case 5:
                    count5++;
                    list5.Add(new GachaStatsItemViewModel(record.Name, pity5, record.Time, GachaType));
                    pity5 = 0;
                    break;
                case 4:
                    count4++;
                    list4.Add(new GachaStatsItemViewModel(record.Name, pity4, record.Time, GachaType));
                    pity4 = 0;
                    break;
                case 3:
                    count3++;
                    break;
            }
        }

        Count5 = count5;
        Count4 = count4;
        Count3 = count3;
        CurrentPity5 = pity5;
        CurrentPity4 = pity4;
        Ratio5 = (double)Count5 / Count;
        Ratio4 = (double)Count4 / Count;
        Ratio3 = (double)Count3 / Count;

        // Starward GachaLogService.cs:202：Average_5 = (Count - Pity_5) / Count_5
        Average5 = Count5 == 0 ? 0 : (double)(Count - CurrentPity5) / Count5;

        // GachaLogService.cs:195-196：列表倒序（最新在前）
        list5.Reverse();
        list4.Reverse();

        // GachaLogService.cs:233-248：首位插入「已垫」占位行
        list5.Insert(0, new GachaStatsItemViewModel(pityText, CurrentPity5, EndTime, GachaType, isPityRow: true));
        list4.Insert(0, new GachaStatsItemViewModel(pityText, CurrentPity4, EndTime, GachaType, isPityRow: true));
        List5 = list5;
        List4 = list4;

        // GachaStatsCard.xaml:101-150 的三行标签（"5★" + 平均 / 统计）
        AverageLabel = "5★" + AppStrings.Instance["Gacha.Average"];
        Stats5Label = "5★" + AppStrings.Instance["Gacha.Stats"];
        Stats4Label = "4★" + AppStrings.Instance["Gacha.Stats"];
        Stats3Label = "3★" + AppStrings.Instance["Gacha.Stats"];
    }


    // Starward GachaTypeStats.Count：这里是去重后的实际抽数。
    private static int ResolveCount(GachaPoolStatistics statistics, IReadOnlyList<GachaRecord> records)
        => records.Count == 0 ? statistics.TotalPulls : records.Count;


    // ── GachaTypeStats 字段 ───────────────────────────────────────────

    public int GachaType { get; }

    /// <summary>卡池类型文本（Starward <c>GachaTypeStats.GachaTypeText</c>，由页面按当前语言给出）。</summary>
    public string GachaTypeText { get; }

    public int Count { get; }

    public string CountText => Count.ToString();

    public DateTime StartTime { get; }

    public DateTime EndTime { get; }

    /// <summary>Starward GachaStatsCard.xaml:69-76 的 "起-止" 一行。</summary>
    public string TimeRangeText { get; }

    public int Count5 { get; }

    public int Count4 { get; }

    public int Count3 { get; }

    public double Ratio5 { get; }

    public double Ratio4 { get; }

    public double Ratio3 { get; }

    /// <summary>Starward <c>GachaTypeStats.Average_5</c>（F2 显示）。</summary>
    public double Average5 { get; }

    /// <summary>距上一发 5★ 的抽数（「已垫」行的值）。</summary>
    public int CurrentPity5 { get; }

    public int CurrentPity4 { get; }

    /// <summary>5★ 列表（首行「已垫」，其余最新在前）。</summary>
    public IReadOnlyList<GachaStatsItemViewModel> List5 { get; }

    /// <summary>4★ 列表（首行「已垫」，其余最新在前）。</summary>
    public IReadOnlyList<GachaStatsItemViewModel> List4 { get; }


    // ── 展示文本（Starward GachaStatsCard.xaml 的 Run 组合） ───────────

    public string AverageLabel { get; }

    public string AverageValueText => Average5.ToString("F2");

    public string Stats5Label { get; }

    public string Stats5ValueText => $"{Count5} [{Ratio5.ToString("P2")}]";

    public string Stats4Label { get; }

    public string Stats4ValueText => $"{Count4} [{Ratio4.ToString("P2")}]";

    public string Stats3Label { get; }

    public string Stats3ValueText => $"{Count3} [{Ratio3.ToString("P2")}]";


    // ── Segmented（Starward 用 cwc:Segmented，这里用两个 ToggleButton 复刻） ──

    /// <summary>选中 5★ 页签（Starward <c>Segmented_GachaItemList.SelectedIndex = 0</c>）。</summary>
    [ObservableProperty]
    private bool _isFiveStarTab = true;

    /// <summary>选中 4★ 页签。</summary>
    [ObservableProperty]
    private bool _isFourStarTab;

    partial void OnIsFiveStarTabChanged(bool value)
    {
        // 单选语义：不能两个都不选（点击已选中的页签时 ToggleButton 会先取消勾选）。
        if (value)
        {
            if (IsFourStarTab)
            {
                IsFourStarTab = false;
            }
        }
        else if (!IsFourStarTab)
        {
            IsFiveStarTab = true;
        }
    }


    partial void OnIsFourStarTabChanged(bool value)
    {
        if (value)
        {
            if (IsFiveStarTab)
            {
                IsFiveStarTab = false;
            }
        }
        else if (!IsFiveStarTab)
        {
            IsFourStarTab = true;
        }
    }


    // ── 悬停高亮（Starward GachaStatsCard.xaml.cs:29-102） ─────────────

    /// <summary>把 5★ 列表里同名的行一起切到悬停态。</summary>
    public void SetPointerIn5(string name, bool value)
    {
        foreach (var item in List5)
        {
            if (string.Equals(item.Name, name, StringComparison.Ordinal))
            {
                item.IsPointerIn = value;
            }
        }
    }


    /// <summary>把 4★ 列表里同名的行一起切到悬停态。</summary>
    public void SetPointerIn4(string name, bool value)
    {
        foreach (var item in List4)
        {
            if (string.Equals(item.Name, name, StringComparison.Ordinal))
            {
                item.IsPointerIn = value;
            }
        }
    }
}
