using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>抽卡统计（纯函数）。</summary>
public class GachaStatisticsTests
{
    private static GachaRecord Record(long id, int rank, int gachaType, DateTime time, string name = "x")
        => new()
        {
            Id = id,
            Name = name,
            ItemType = rank == 5 ? "角色" : "武器",
            RankType = rank,
            GachaType = gachaType,
            GachaTypeName = GachaService.GetPoolName("genshin", gachaType),
            Time = time,
            Uid = 100000000,
        };


    [Fact]
    public void EmptyRecords_NoPools()
    {
        Assert.Empty(GachaService.ComputeStatistics([]));
    }


    [Fact]
    public void CountsPullsAndRaritiesPerPool()
    {
        var t = new DateTime(2026, 1, 1);
        var records = new[]
        {
            Record(1, 3, 301, t),
            Record(2, 4, 301, t.AddMinutes(1)),
            Record(3, 5, 301, t.AddMinutes(2)),
            Record(4, 3, 200, t.AddMinutes(3)),
        };

        var stats = GachaService.ComputeStatistics(records);

        var limited = stats.Single(s => s.GachaType == 301);
        Assert.Equal(3, limited.TotalPulls);
        Assert.Equal(1, limited.FiveStarCount);
        Assert.Equal(1, limited.FourStarCount);

        var standard = stats.Single(s => s.GachaType == 200);
        Assert.Equal(1, standard.TotalPulls);
        Assert.Equal(0, standard.FiveStarCount);
    }


    [Fact]
    public void CurrentPity_CountsPullsSinceLastFiveStar()
    {
        var t = new DateTime(2026, 1, 1);
        var records = new List<GachaRecord>();
        var id = 1L;

        // 89 发非 5 星，然后 1 发 5 星，再 12 发非 5 星
        for (var i = 0; i < 89; i++)
        {
            records.Add(Record(id++, 3, 301, t.AddMinutes(i)));
        }

        records.Add(Record(id++, 5, 301, t.AddMinutes(89)));
        for (var i = 0; i < 12; i++)
        {
            records.Add(Record(id++, 3, 301, t.AddMinutes(90 + i)));
        }

        var stats = GachaService.ComputeStatistics(records).Single();

        Assert.Equal(102, stats.TotalPulls);
        Assert.Equal(1, stats.FiveStarCount);
        Assert.Equal(12, stats.CurrentPity);
        // 第一个 5 星出现在第 90 抽
        Assert.Equal([90], stats.FiveStarPities);
        Assert.Equal(90.0, stats.AveragePity);
    }


    [Fact]
    public void NoFiveStar_CurrentPityEqualsTotalPulls()
    {
        var t = new DateTime(2026, 1, 1);
        var records = Enumerable.Range(0, 40)
            .Select(i => Record(i + 1, 3, 200, t.AddMinutes(i)))
            .ToList();

        var stats = GachaService.ComputeStatistics(records).Single();
        Assert.Equal(40, stats.CurrentPity);
        Assert.Empty(stats.FiveStarPities);
        Assert.Equal(0, stats.AveragePity);
    }


    [Fact]
    public void DuplicateIds_AreDeduped()
    {
        var t = new DateTime(2026, 1, 1);
        var records = new[]
        {
            Record(1, 3, 301, t),
            Record(1, 3, 301, t),  // 分页重叠
            Record(2, 5, 301, t.AddMinutes(1)),
        };

        var stats = GachaService.ComputeStatistics(records).Single();
        Assert.Equal(2, stats.TotalPulls);
        Assert.Equal(1, stats.FiveStarCount);
    }


    [Fact]
    public void OrderIsByTimeNotById()
    {
        var t = new DateTime(2026, 1, 1);
        // 故意让 id 与时间顺序相反
        var records = new[]
        {
            Record(100, 5, 301, t.AddMinutes(2)),
            Record(200, 3, 301, t.AddMinutes(1)),
        };

        var stats = GachaService.ComputeStatistics(records).Single();
        // 按时间：先 3 星（pity=1），后 5 星（pity=2）
        Assert.Equal([2], stats.FiveStarPities);
        Assert.Equal(0, stats.CurrentPity);
    }


    [Fact]
    public void MultiplePools_AreSeparated()
    {
        var t = new DateTime(2026, 1, 1);
        var records = new[]
        {
            Record(1, 5, 301, t),
            Record(2, 3, 302, t.AddMinutes(1)),
            Record(3, 3, 302, t.AddMinutes(2)),
        };

        var stats = GachaService.ComputeStatistics(records);
        Assert.Equal(2, stats.Count);
        Assert.Equal(1, stats.Single(s => s.GachaType == 301).TotalPulls);
        Assert.Equal(2, stats.Single(s => s.GachaType == 302).TotalPulls);
    }


    [Fact]
    public void MultipleFiveStars_AverageIsMeanOfPities()
    {
        var t = new DateTime(2026, 1, 1);
        var records = new List<GachaRecord>();
        var id = 1L;

        void AddPulls(int count, bool five = false)
        {
            for (var i = 0; i < count; i++)
            {
                records.Add(Record(id++, five && i == count - 1 ? 5 : 3, 301, t.AddMinutes(id)));
            }
        }

        AddPulls(76, five: true);   // 第 76 抽
        AddPulls(80, five: true);   // 再来 80 抽

        var stats = GachaService.ComputeStatistics(records).Single();
        Assert.Equal(2, stats.FiveStarCount);
        Assert.Equal([76, 80], stats.FiveStarPities);
        Assert.Equal(78.0, stats.AveragePity);
        Assert.Equal(0, stats.CurrentPity);
    }


    [Fact]
    public void GetFiveStars_NewestFirstAndLimited()
    {
        var t = new DateTime(2026, 1, 1);
        var records = Enumerable.Range(0, 10)
            .Select(i => Record(i + 1, i % 2 == 0 ? 5 : 3, 301, t.AddMinutes(i)))
            .ToList();

        var fiveStars = GachaService.GetFiveStars(records, max: 3);
        Assert.Equal(3, fiveStars.Count);
        Assert.True(fiveStars[0].Time > fiveStars[1].Time);
        Assert.All(fiveStars, r => Assert.True(r.IsFiveStar));
    }


    [Fact]
    public void GetUid_ReturnsFirstPositive()
    {
        var t = new DateTime(2026, 1, 1);
        var records = new[]
        {
            Record(1, 3, 301, t) with { Uid = 0 },
            Record(2, 3, 301, t.AddMinutes(1)) with { Uid = 123456789 },
        };

        Assert.Equal(123456789, GachaService.GetUid(records));
        Assert.Null(GachaService.GetUid([]));
    }


    [Theory]
    [InlineData("genshin", 100, "新手祈愿")]
    [InlineData("genshin", 200, "常驻祈愿")]
    [InlineData("genshin", 301, "角色活动祈愿")]
    [InlineData("genshin", 302, "武器活动祈愿")]
    [InlineData("genshin", 500, "集录祈愿")]
    [InlineData("zzz", 1, "常驻频段")]
    [InlineData("zzz", 2, "独家频段")]
    [InlineData("zzz", 3, "音擎频段")]
    [InlineData("zzz", 5, "邦布频段")]
    [InlineData("genshin", 999, "未知卡池(999)")]
    public void GetPoolName_MapsKnownTypes(string gameKind, int type, string expected)
    {
        Assert.Equal(expected, GachaService.GetPoolName(gameKind, type));
    }


    [Fact]
    public void FindGachaUrl_ReportsReasonWhenDirectoryMissing()
    {
        var url = GachaService.FindGachaUrl("/definitely/not/here", new LunarWard.Core.GameBiz("hk4e_cn"), out var error);
        Assert.Null(url);
        Assert.Contains("游戏目录不存在", error);
    }


    [Fact]
    public void FindGachaUrl_ReportsWhenCacheHasNoLink()
    {
        using var temp = new TempDirectory();
        var game = temp.Combine("game");
        Directory.CreateDirectory(Path.Join(game, "YuanShen_Data", "webCaches", "Cache", "Cache_Data"));
        File.WriteAllText(Path.Join(game, "YuanShen_Data", "webCaches", "Cache", "Cache_Data", "data_2"), "nothing here");

        var url = GachaService.FindGachaUrl(game, new LunarWard.Core.GameBiz("hk4e_cn"), out var error);
        Assert.Null(url);
        Assert.Contains("祈愿", error);
    }
}
