using LunarWard.Core.SelfQuery;
using LunarWard.Launch.Database;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 账号记录（<see cref="SelfQueryLogStore"/>）的持久化语义：与上游 Starward 一样走 SQLite，
/// 复刻同样的「按 uid + type 去重、按月统计/删除、检索全部时整月重写」行为。
/// </summary>
public sealed class SelfQueryLogStoreTests : IDisposable
{

    private readonly string _directory;

    private readonly string _database;


    public SelfQueryLogStoreTests()
    {
        _directory = Path.Join(Path.GetTempPath(), "lunarward-selfquery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _database = Path.Join(_directory, "LunarWardDatabase.db");
    }


    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }
    }


    private SelfQueryLogStore NewStore() => new(new LunarWardDatabase(_database));


    private static GenshinQueryItem Item(long uid, long id, long addNum, DateTime time, GenshinQueryType type = GenshinQueryType.Primogem, string reason = "每日委托")
        => new()
        {
            Uid = uid,
            Id = id,
            AddNum = addNum,
            DateTime = time,
            Time = time,
            Type = type,
            Reason = reason,
            Name = "",
            Icon = "",
        };


    [Fact]
    public void Insert_ThenReadBack_PersistsAcrossInstances()
    {
        var time = new DateTime(2026, 10, 20, 9, 12, 0);
        NewStore().InsertGenshinQueryItems(100000001, GenshinQueryType.Primogem, [Item(100000001, 1, 160, time)]);

        var store = NewStore();
        Assert.Equal([100000001L], store.GetGenshinUids());
        var items = store.GetGenshinQueryItems(100000001, GenshinQueryType.Primogem, "2026-10");
        var item = Assert.Single(items);
        Assert.Equal(160, item.AddNum);
        Assert.Equal(new DateTime(2026, 10, 20, 9, 12, 0), item.DateTime);
    }


    [Fact]
    public void SameId_IsOverwritten()
    {
        var time = new DateTime(2026, 10, 20);
        var store = NewStore();
        store.InsertGenshinQueryItems(1, GenshinQueryType.Primogem, [Item(1, 7, 10, time)]);
        store.InsertGenshinQueryItems(1, GenshinQueryType.Primogem, [Item(1, 7, 99, time)]);

        Assert.Equal(99, store.GetGenshinQueryItemsNumSum(1, GenshinQueryType.Primogem).Add);
    }


    [Fact]
    public void Months_AreDistinctAndDescending()
    {
        var store = NewStore();
        store.InsertGenshinQueryItems(1, GenshinQueryType.Primogem,
        [
            Item(1, 1, 10, new DateTime(2026, 9, 30)),
            Item(1, 2, 10, new DateTime(2026, 10, 1)),
            Item(1, 3, 10, new DateTime(2026, 10, 2)),
        ]);

        Assert.Equal(["2026-10", "2026-09"], store.GetGenshinMonths(1, GenshinQueryType.Primogem));
    }


    [Fact]
    public void DeleteMonth_RemovesOnlyThatMonth()
    {
        var store = NewStore();
        store.InsertGenshinQueryItems(1, GenshinQueryType.Primogem,
        [
            Item(1, 1, 10, new DateTime(2026, 9, 30)),
            Item(1, 2, 10, new DateTime(2026, 10, 1)),
        ]);

        store.DeleteGenshinMonth(1, GenshinQueryType.Primogem, "2026-09");

        Assert.Single(store.GetGenshinQueryItems(1, GenshinQueryType.Primogem, null));
        Assert.Equal(["2026-10"], store.GetGenshinMonths(1, GenshinQueryType.Primogem));
    }


    [Fact]
    public void InsertAll_RewritesTouchedMonthsOnly()
    {
        var store = NewStore();
        store.InsertGenshinQueryItems(1, GenshinQueryType.Primogem, [Item(1, 1, 10, new DateTime(2026, 9, 30))]);

        // all: true 只清「本批涉及的月份」（2026-10），9 月的行要保留。
        store.InsertGenshinQueryItems(1, GenshinQueryType.Primogem,
            [Item(1, 2, 20, new DateTime(2026, 10, 1))], all: true);

        Assert.Equal(30, store.GetGenshinQueryItemsNumSum(1, GenshinQueryType.Primogem).Add);
        Assert.Equal(2, store.GetGenshinQueryItems(1, GenshinQueryType.Primogem, null).Count);
    }


    [Fact]
    public void DifferentUid_AndDifferentType_AreIsolated()
    {
        var store = NewStore();
        var time = new DateTime(2026, 10, 20);
        store.InsertGenshinQueryItems(1, GenshinQueryType.Weapon, [Item(1, 1, 5, time, GenshinQueryType.Weapon)]);
        store.InsertGenshinQueryItems(2, GenshinQueryType.Weapon, [Item(2, 1, 7, time, GenshinQueryType.Weapon)]);

        Assert.Equal(5, store.GetGenshinQueryItemsNumSum(1, GenshinQueryType.Weapon).Add);
        Assert.Equal(7, store.GetGenshinQueryItemsNumSum(2, GenshinQueryType.Weapon).Add);
        Assert.Equal(0, store.GetGenshinQueryItemsNumSum(1, GenshinQueryType.Artifact).Add);
    }


    [Fact]
    public void StarRail_UsesAutoIncrementId_AndMonthFilter()
    {
        var store = NewStore();
        store.InsertStarRailQueryItems(100, StarRailQueryType.Stellar,
        [
            new StarRailQueryItem { Id = 1, Uid = 100, Type = StarRailQueryType.Stellar, AddNum = 60, Time = new DateTime(2026, 10, 1, 8, 0, 0), Action = "月卡" },
            new StarRailQueryItem { Id = 2, Uid = 100, Type = StarRailQueryType.Stellar, AddNum = -160, Time = new DateTime(2026, 10, 2, 8, 0, 0), Action = "抽卡" },
        ]);

        Assert.Equal((60L, -160L), store.GetStarRailQueryItemsNumSum(100, StarRailQueryType.Stellar));
        Assert.Equal(2, store.GetStarRailLastId(100, StarRailQueryType.Stellar));
        Assert.Equal(["2026-10"], store.GetStarRailMonths(100, StarRailQueryType.Stellar));
    }


    [Fact]
    public void Zzz_RoundTrips()
    {
        var store = NewStore();
        store.InsertZZZQueryItems(200, ZZZQueryType.Monochrome,
        [
            new ZZZQueryItem { Id = 1, Uid = 200, Type = ZZZQueryType.Monochrome, AddNum = 60, DateTime = new DateTime(2026, 10, 3), Reason = "每日" },
        ]);

        var item = Assert.Single(store.GetZZZQueryItems(200, ZZZQueryType.Monochrome, "2026-10"));
        Assert.Equal(60, item.AddNum);
        Assert.Equal("每日", item.Reason);
    }

}
