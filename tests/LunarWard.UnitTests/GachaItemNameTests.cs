using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 抽卡记录改名（Starward <c>ChangeGachaItemNameCommand</c> 的纯映射部分）。
/// </summary>
public class GachaItemNameTests
{
    private static GachaRecord Record(long id, int itemId, string name = "旧名字") => new()
    {
        Id = id,
        Name = name,
        ItemType = "角色",
        RankType = 5,
        GachaType = 301,
        GachaTypeName = "角色活动祈愿",
        Time = new DateTime(2026, 1, 2, 3, 4, 5),
        Uid = 100000001,
        ItemId = itemId,
    };


    [Fact]
    public void ApplyItemNamesRenamesOnlyKnownIds()
    {
        var names = new Dictionary<int, string>
        {
            [10000002] = "琴",
        };

        var (records, renamed) = GachaService.ApplyItemNames(
            [Record(1, 10000002), Record(2, 99999999)],
            names);

        Assert.Equal(1, renamed);
        Assert.Equal("琴", records[0].Name);
        Assert.Equal("旧名字", records[1].Name);
    }


    [Fact]
    public void ApplyItemNamesIgnoresBlankNamesAndKeepsOrderAndFields()
    {
        var names = new Dictionary<int, string>
        {
            [10000002] = "  ",
            [10000003] = "琴",
            [10000004] = "旧名字",   // 与现名相同 → 不算改名
        };

        var input = new[] { Record(1, 10000002), Record(2, 10000003), Record(3, 10000004) };
        var (records, renamed) = GachaService.ApplyItemNames(input, names);

        Assert.Equal(1, renamed);
        Assert.Equal("旧名字", records[0].Name);
        Assert.Equal("琴", records[1].Name);
        Assert.Equal("旧名字", records[2].Name);
        // 其它字段原样保留
        Assert.Equal(input[1].Id, records[1].Id);
        Assert.Equal(input[1].Uid, records[1].Uid);
        Assert.Equal(input[1].Time, records[1].Time);
    }


    [Fact]
    public void ApplyItemNamesWithEmptyTableChangesNothing()
    {
        var (records, renamed) = GachaService.ApplyItemNames([Record(1, 10000002)], new Dictionary<int, string>());

        Assert.Equal(0, renamed);
        Assert.Equal("旧名字", records[0].Name);
    }
}
