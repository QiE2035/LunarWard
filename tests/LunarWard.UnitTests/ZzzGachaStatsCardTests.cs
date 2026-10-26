using LunarWard.ViewModels.Starward;
using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 绝区零统计卡的标签与 UP 平均（Starward <c>ZZZGachaStatsCard.xaml</c> +
/// <c>GachaTypeStats.Avarage_5_Desc_Text / Avarage_5_Up_Text</c>）。
/// </summary>
public class ZzzGachaStatsCardTests
{
    private static GachaRecord Record(long id, int rank, DateTime time, bool isUp = false, string name = "x")
        => new()
        {
            Id = id,
            Name = name,
            ItemType = rank == 5 ? "音擎" : "材料",
            RankType = rank,
            GachaType = 2,
            GachaTypeName = GachaService.GetPoolName("zzz", 2),
            Time = time,
            Uid = 100000001,
            IsUp = isUp,
        };


    private static GachaPoolCardViewModel Card(IReadOnlyList<GachaRecord> records, bool zzz)
    {
        var statistics = GachaService.ComputeStatistics(records).Single();
        return new GachaPoolCardViewModel(statistics, records, "独家频段", "已垫", zzz: zzz);
    }


    [Fact]
    public void ZzzCardUsesRankLetters()
    {
        var t = new DateTime(2026, 1, 1);
        var records = new[]
        {
            Record(1, 3, t),
            Record(2, 2, t),
            Record(3, 5, t.AddMinutes(1), isUp: true, name: "耀嘉音"),
        };

        var card = Card(records, zzz: true);

        Assert.Equal("S平均", card.AverageLabel);
        Assert.Equal("S统计", card.Stats5Label);
        Assert.Equal("A统计", card.Stats4Label);
        Assert.Equal("B统计", card.Stats3Label);

        // 非绝区零仍是 5★/4★/3★
        var genshin = Card(records, zzz: false);
        Assert.Equal("5★平均", genshin.AverageLabel);
        Assert.Equal("3★统计", genshin.Stats3Label);
    }


    [Fact]
    public void UpAverageCountsOnlyUpFiveStars()
    {
        var t = new DateTime(2026, 1, 1);
        var records = new List<GachaRecord>();
        long id = 0;

        // 10 抽到 5★（非 UP），再来 20 抽到 5★（UP），合计 30 抽
        for (int i = 0; i < 9; i++)
        {
            records.Add(Record(++id, 3, t.AddMinutes(id)));
        }
        records.Add(Record(++id, 5, t.AddMinutes(id), isUp: false, name: "月城柳"));
        for (int i = 0; i < 19; i++)
        {
            records.Add(Record(++id, 3, t.AddMinutes(id)));
        }
        records.Add(Record(++id, 5, t.AddMinutes(id), isUp: true, name: "耀嘉音"));

        var card = Card(records, zzz: true);

        Assert.Equal(2, card.Count5);
        Assert.Equal(1, card.Count5Up);
        Assert.Equal(20, card.Average5Up, 3);
        Assert.Equal(" / UP", card.AverageDescText);
        Assert.Equal(" / 20.00 (1)", card.AverageUpText);
        Assert.True(card.List5[1].HasUpItem);
        Assert.False(card.List5[2].HasUpItem);
    }


    [Fact]
    public void NoUpFiveStarsHidesUpSuffix()
    {
        var t = new DateTime(2026, 1, 1);
        var records = new[]
        {
            Record(1, 3, t),
            Record(2, 5, t.AddMinutes(1), name: "月城柳"),
        };

        var card = Card(records, zzz: true);

        Assert.Equal(0, card.Count5Up);
        Assert.Equal("", card.AverageDescText);
        Assert.Equal("", card.AverageUpText);
    }

}
