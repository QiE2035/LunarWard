using LunarWard.Core.Gacha.Genshin;
using LunarWard.Launch.Database;
using LunarWard.Launch.Gacha;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 千星奇域颂愿（Starward <c>GenshinBeyondGachaService</c>）的统计口径。
///
/// <para>上游用 SQLite，LunarWard 也改用同一个 <c>LunarWardDatabase.db</c>；这里只验证「统计结果」与源码逐行一致：
/// 1000 卡按 4★ 计保底与平均、2000 卡按 5★，3★ 的保底单独统计，
/// 列表时间倒序后头部还有一条「保底」占位行。</para>
/// </summary>
public class GenshinBeyondGachaTests
{

    private const long Uid = 100000001;


    private static GenshinBeyondGachaService CreateService(out string file)
    {
        file = Path.Join(Path.GetTempPath(), $"lw-beyond-{Guid.NewGuid():N}", LunarWardDatabase.FileName);
        return new GenshinBeyondGachaService(databaseFile: file);
    }


    private static GenshinBeyondGachaStore CreateStore(out string file)
    {
        file = Path.Join(Path.GetTempPath(), $"lw-beyond-{Guid.NewGuid():N}", LunarWardDatabase.FileName);
        return new GenshinBeyondGachaStore(new LunarWardDatabase(file));
    }


    /// <summary>同一个文件上建 store 与 service（service 每次调用都会重新读文件）。</summary>
    private static (GenshinBeyondGachaStore Store, GenshinBeyondGachaService Service) CreateBoth()
    {
        var file = Path.Join(Path.GetTempPath(), $"lw-beyond-{Guid.NewGuid():N}", LunarWardDatabase.FileName);
        return (new GenshinBeyondGachaStore(new LunarWardDatabase(file)), new GenshinBeyondGachaService(databaseFile: file));
    }


    private static GenshinBeyondGachaItemEx Item(long id, int rank, int opGachaType, int minute, string name = "x", int itemId = 0)
        => new()
        {
            Uid = Uid,
            Id = id,
            Region = "cn_gf01",
            OpGachaType = opGachaType,
            ScheduleId = 1,
            ItemType = "材料",
            ItemId = itemId == 0 ? (int)id + 10000 : itemId,
            ItemName = name,
            RankType = rank,
            IsUp = 0,
            Time = new DateTime(2026, 10, 1, 12, 0, 0).AddMinutes(minute),
        };


    [Fact]
    public void Type1000_PityAndAverageFollowFourStar()
    {
        var (store, service) = CreateBoth();
        store.InsertItems([
            Item(1, 3, 1000, 0),
            Item(2, 4, 1000, 1),
            Item(3, 3, 1000, 2),
            Item(4, 3, 1000, 3),
            Item(5, 3, 1000, 4),
        ]);

        var stats = service.GetGachaTypeStatsType1000(Uid);

        Assert.NotNull(stats);
        Assert.Equal(5, stats.Count);
        Assert.Equal(1, stats.Count_4);
        Assert.Equal(4, stats.Count_3);
        Assert.Equal(0.2, stats.Ratio_4, 6);
        // 最后一抽是 3★：保底 = 自上一发 4★ 起累计 3 抽
        Assert.Equal(3, stats.Pity_4);
        Assert.Equal(2.0, stats.Average_4, 6);
        // 最后一抽本身就是 3★（index 4），所以 3★ 保底为 0
        Assert.Equal(0, stats.Pity_3);
        // 头部占位行 + 1 条 4★；占位行的 Pity 就是当前保底
        Assert.Equal(2, stats.List_4.Count);
        Assert.Equal(0, stats.List_4[0].Id);
        Assert.Equal(3, stats.List_4[0].Pity);
        Assert.Equal(5, stats.List_3.Count);
        Assert.Equal(0, stats.List_3[0].Id);
    }


    [Fact]
    public void Type1000_LastPullIsFourStar_PityResetsToZero()
    {
        var (store, service) = CreateBoth();
        store.InsertItems([
            Item(1, 3, 1000, 0),
            Item(2, 4, 1000, 1),
        ]);

        var stats = service.GetGachaTypeStatsType1000(Uid);

        Assert.NotNull(stats);
        Assert.Equal(0, stats.Pity_4);
        Assert.Equal(2.0, stats.Average_4, 6);
    }


    [Fact]
    public void Type2000_PityAndAverageFollowFiveStar()
    {
        var (store, service) = CreateBoth();
        store.InsertItems([
            Item(1, 3, 2000, 0),
            Item(2, 4, 2000, 1),
            Item(3, 5, 2000, 2),
            Item(4, 3, 2000, 3),
        ]);

        var stats = service.GetGachaTypeStatsType2000(Uid);

        Assert.NotNull(stats);
        Assert.Equal(4, stats.Count);
        Assert.Equal(1, stats.Count_5);
        // 最后一抽是 3★：自上一发 5★ 起累计 1 抽
        Assert.Equal(1, stats.Pity_5);
        Assert.Equal(3.0, stats.Average_5, 6);
        Assert.Equal(2, stats.List_5.Count);
        Assert.Equal(1, stats.List_5[0].Pity);
        // 第二页是 4★：上一发 4★ 之后又抽了 1 次 5★ 与 1 次 3★ → 2 抽
        Assert.Equal(2, stats.Pity_4);
    }


    [Fact]
    public void Type2000_IgnoresType1000Rows()
    {
        var (store, service) = CreateBoth();
        store.InsertItems([
            Item(1, 3, 1000, 0),
            Item(2, 4, 1000, 1),
            Item(3, 3, 2000, 2),
        ]);

        var stats = service.GetGachaTypeStatsType2000(Uid);

        Assert.NotNull(stats);
        Assert.Equal(1, stats.Count);
        // 没有 5★ 时列表里只有头部占位行
        Assert.Single(stats.List_5);
        Assert.Equal(0, stats.List_5[0].Id);
    }


    [Fact]
    public void ItemStats_GroupsByItemIdAndSortsByRankThenCount()
    {
        var (store, service) = CreateBoth();
        store.InsertItems([
            Item(1, 3, 1000, 0, "甲", itemId: 500),
            Item(2, 3, 1000, 1, "甲", itemId: 500),
            Item(3, 4, 1000, 2, "乙", itemId: 600),
            Item(4, 3, 2000, 3, "丙", itemId: 500),
        ]);

        var items = service.GetGachaItemStats(Uid)
                    ?? throw new InvalidOperationException("GetGachaItemStats 不应返回 null");

        // 500 出现 3 次（两抽 1000 + 一抽 2000），600 出现 1 次；4★ 排最前
        Assert.Equal(2, items.Count);
        // 列表元素类型可空（分组键缺失时会跳过），这里按契约断言非空。
        var famous = items[0]!;
        var common = items[1]!;
        Assert.Equal(600, famous.ItemId);
        Assert.Equal(1, famous.Count);
        Assert.Equal(500, common.ItemId);
        Assert.Equal(3, items[1].Count);
    }


    [Fact]
    public void InsertItems_DeduplicatesById()
    {
        var (store, service) = CreateBoth();
        store.InsertItems([Item(1, 3, 1000, 0)]);
        store.InsertItems([Item(1, 3, 1000, 0), Item(2, 3, 1000, 1)]);

        Assert.Equal(2, store.CountItems(Uid));
    }


    [Fact]
    public void DeleteByTime_RemovesOnlyMatchingWindow()
    {
        var (store, service) = CreateBoth();
        store.InsertItems([
            Item(1, 3, 1000, 0),
            Item(2, 3, 1000, 60),
            Item(3, 3, 1000, 120),
        ]);

        store.DeleteByTime(Uid, new DateTime(2026, 10, 1, 12, 0, 0), new DateTime(2026, 10, 1, 12, 30, 0));

        Assert.Equal(2, store.CountItems(Uid));
    }
}
