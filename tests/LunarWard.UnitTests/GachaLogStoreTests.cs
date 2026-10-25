using LunarWard.Launch;
using LunarWard.Launch.Database;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 抽卡记录 SQLite 存储（Starward <c>GachaLogService</c> 的 <c>INSERT OR REPLACE</c> /
/// <c>DELETE ... WHERE Time BETWEEN</c> 语义）。
/// </summary>
public sealed class GachaLogStoreTests : IDisposable
{
    private readonly string _dir = Path.Join(Path.GetTempPath(), "lunarward-gachadb-" + Guid.NewGuid().ToString("N"));

    private readonly GachaLogStore _store;


    public GachaLogStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new GachaLogStore(new LunarWardDatabase(Path.Join(_dir, LunarWardDatabase.FileName)));
    }


    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }


    private static GachaRecord Record(long id, DateTime time, long uid = 100000001, int rank = 3)
        => new()
        {
            Id = id,
            Name = "测试物品",
            ItemType = "角色",
            RankType = rank,
            GachaType = 301,
            GachaTypeName = GachaService.GetPoolName(GachaLogStore.Genshin, 301),
            Time = time,
            Uid = uid,
            ItemId = 10000002,
        };


    [Fact]
    public void MergeReplacesSameIdAndKeepsOthers()
    {
        var t = new DateTime(2026, 1, 2, 3, 4, 5);

        _store.Merge(GachaLogStore.Genshin, [Record(1, t), Record(2, t.AddMinutes(1))], "zh-cn");
        _store.Merge(GachaLogStore.Genshin, [Record(2, t.AddMinutes(1), rank: 5)], "zh-cn");

        var items = _store.GetItems(GachaLogStore.Genshin, 100000001);

        Assert.Equal(2, items.Count);
        Assert.Equal(5, items[1].RankType);
        Assert.Equal("zh-cn", _store.GetLang(GachaLogStore.Genshin, 100000001));
    }


    [Fact]
    public void GetSummariesGroupsByUidAndKeepsNewestName()
    {
        var t = new DateTime(2026, 1, 2, 3, 4, 5);

        _store.Merge(GachaLogStore.Genshin, [Record(1, t)], "zh-cn");
        _store.Merge(GachaLogStore.Genshin, [Record(9, t.AddHours(1), uid: 200000002)], "zh-cn");

        var summaries = _store.GetSummaries();

        Assert.Equal(2, summaries.Count);
        Assert.Equal([100000001L, 200000002L], summaries.Select(x => x.Uid));
        Assert.Equal(t.AddHours(1), summaries[1].LastItemTime);
    }


    [Fact]
    public void DeleteRangeRemovesOnlyMatchingUidAndInclusiveRange()
    {
        var t = new DateTime(2026, 1, 1, 10, 0, 0);
        _store.Merge(GachaLogStore.Genshin,
        [
            Record(1, t),
            Record(2, t.AddHours(1)),
            Record(3, t.AddHours(2)),
            Record(4, t, uid: 200000002),
        ], "zh-cn");

        int removed = _store.DeleteRange(GachaLogStore.Genshin, 100000001, t, t.AddHours(1));

        Assert.Equal(2, removed);
        var left = _store.GetItems(GachaLogStore.Genshin, 100000001);
        Assert.Single(left);
        Assert.Equal(3, left[0].Id);
        Assert.Single(_store.GetItems(GachaLogStore.Genshin, 200000002));
    }


    [Fact]
    public void DeleteUidRemovesAllRecordsOfUid()
    {
        var t = new DateTime(2026, 1, 1);
        _store.Merge(GachaLogStore.Genshin, [Record(1, t), Record(2, t), Record(3, t, uid: 200000002)], "zh-cn");

        Assert.Equal(2, _store.DeleteUid(GachaLogStore.Genshin, 100000001));
        Assert.Empty(_store.GetItems(GachaLogStore.Genshin, 100000001));
        Assert.Single(_store.GetAll(GachaLogStore.Genshin));
    }


    [Fact]
    public void GachaLogUrlIsSavedPerGameBiz()
    {
        _store.SaveGachaLogUrl("hk4e_cn", 100000001, "https://example.invalid/?authkey=1");
        _store.SaveGachaLogUrl("hk4e_cn", 100000001, "https://example.invalid/?authkey=2");

        Assert.Equal("https://example.invalid/?authkey=2", _store.GetGachaLogUrl("hk4e_cn"));
        Assert.Null(_store.GetGachaLogUrl("hkrpg_cn"));
    }


    [Fact]
    public void StarRailRecordsRoundTripWithGachaIdColumn()
    {
        var t = new DateTime(2026, 1, 2, 3, 4, 5);
        _store.Merge(GachaLogStore.StarRail, [Record(1, t)], "zh-cn");

        var item = Assert.Single(_store.GetItems(GachaLogStore.StarRail, 100000001));

        Assert.Equal(t, item.Time);
        Assert.Equal("测试物品", item.Name);
    }
}
