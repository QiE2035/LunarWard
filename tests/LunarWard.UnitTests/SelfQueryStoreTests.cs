using LunarWard.Core.SelfQuery;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 账号记录（<see cref="SelfQueryStore"/>）的持久化语义：上游 Starward 用 SQLite，
/// 这里必须复刻同样的「按 uid + type 去重、按月统计/删除、检索全部时整月重写」行为。
/// </summary>
public sealed class SelfQueryStoreTests : IDisposable
{

    private readonly string _directory;

    private readonly string _file;


    public SelfQueryStoreTests()
    {
        _directory = Path.Join(Path.GetTempPath(), "lunarward-selfquery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _file = Path.Join(_directory, "selfquery.json");
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
        new SelfQueryStore(_file).InsertGenshinQueryItems(100000001, GenshinQueryType.Primogem, [Item(100000001, 1, 160, time)]);

        var store = new SelfQueryStore(_file);
        Assert.Equal(new[] { 100000001L }, store.GetGenshinUids());
        Assert.Single(store.GetGenshinQueryItems(100000001, GenshinQueryType.Primogem, null));
        Assert.Equal(1, store.GetGenshinLastId(100000001, GenshinQueryType.Primogem));
    }


    [Fact]
    public void Insert_SameUidTypeId_ReplacesInsteadOfDuplicating()
    {
        var store = new SelfQueryStore(_file);
        var time = new DateTime(2026, 10, 20, 9, 0, 0);
        store.InsertGenshinQueryItems(1, GenshinQueryType.Primogem, [Item(1, 10, 160, time)]);
        store.InsertGenshinQueryItems(1, GenshinQueryType.Primogem, [Item(1, 10, 10, time)]);

        var items = store.GetGenshinQueryItems(1, GenshinQueryType.Primogem, null);
        Assert.Single(items);
        Assert.Equal(10, items[0].AddNum);
    }


    [Fact]
    public void NumSum_SeparatesIncomeAndExpense()
    {
        var store = new SelfQueryStore(_file);
        var time = new DateTime(2026, 10, 20);
        store.InsertGenshinQueryItems(1, GenshinQueryType.Primogem,
        [
            Item(1, 1, 160, time),
            Item(1, 2, 140, time),
            Item(1, 3, -1600, time, GenshinQueryType.Primogem, "纠缠之缘 ×10"),
        ]);

        var (add, sub) = store.GetGenshinQueryItemsNumSum(1, GenshinQueryType.Primogem);
        Assert.Equal(300, add);
        Assert.Equal(-1600, sub);
    }


    [Fact]
    public void Months_AreDistinctAndNewestFirst()
    {
        var store = new SelfQueryStore(_file);
        store.InsertGenshinQueryItems(1, GenshinQueryType.Resin,
        [
            Item(1, 1, 60, new DateTime(2026, 9, 30), GenshinQueryType.Resin),
            Item(1, 2, 60, new DateTime(2026, 10, 1), GenshinQueryType.Resin),
            Item(1, 3, 60, new DateTime(2026, 10, 2), GenshinQueryType.Resin),
        ]);

        Assert.Equal(["2026-10", "2026-09"], store.GetGenshinMonths(1, GenshinQueryType.Resin));
    }


    [Fact]
    public void DeleteMonth_RemovesOnlyThatMonth()
    {
        var store = new SelfQueryStore(_file);
        store.InsertGenshinQueryItems(1, GenshinQueryType.Resin,
        [
            Item(1, 1, 60, new DateTime(2026, 9, 30), GenshinQueryType.Resin),
            Item(1, 2, 60, new DateTime(2026, 10, 1), GenshinQueryType.Resin),
        ]);

        store.DeleteGenshinMonth(1, GenshinQueryType.Resin, "2026-10");

        Assert.Equal(["2026-09"], store.GetGenshinMonths(1, GenshinQueryType.Resin));
        Assert.Single(store.GetGenshinQueryItems(1, GenshinQueryType.Resin, null));
    }


    [Fact]
    public void InsertAll_ClearsInvolvedMonthsBeforeWriting()
    {
        var store = new SelfQueryStore(_file);
        var october = new DateTime(2026, 10, 5);
        store.InsertGenshinQueryItems(1, GenshinQueryType.Crystal,
        [
            Item(1, 1, 300, october, GenshinQueryType.Crystal),
            Item(1, 2, 300, october, GenshinQueryType.Crystal),
        ]);

        // 检索全部：同一批数据会把 2026-10 整月清掉再写，因此只应剩一条。
        store.InsertGenshinQueryItems(1, GenshinQueryType.Crystal, [Item(1, 1, 300, october, GenshinQueryType.Crystal)], all: true);

        Assert.Single(store.GetGenshinQueryItems(1, GenshinQueryType.Crystal, "2026-10"));
    }


    [Fact]
    public void DifferentUid_AndDifferentType_AreIsolated()
    {
        var store = new SelfQueryStore(_file);
        var time = new DateTime(2026, 10, 20);
        store.InsertGenshinQueryItems(1, GenshinQueryType.Weapon, [Item(1, 1, 5, time, GenshinQueryType.Weapon)]);
        store.InsertGenshinQueryItems(2, GenshinQueryType.Weapon, [Item(2, 1, 7, time, GenshinQueryType.Weapon)]);

        Assert.Equal(5, store.GetGenshinQueryItemsNumSum(1, GenshinQueryType.Weapon).Add);
        Assert.Equal(7, store.GetGenshinQueryItemsNumSum(2, GenshinQueryType.Weapon).Add);
        Assert.Equal(0, store.GetGenshinQueryItemsNumSum(1, GenshinQueryType.Artifact).Add);
    }

}
