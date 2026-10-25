using LunarWard.Core.Gacha;
using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// <see cref="GachaNoUp.ComputeIsUp"/> 与 <see cref="GachaService.ApplyUpFlags"/>：
/// 上游 <c>GenshinGachaService.GetGachaLogItemEx</c> 的「常驻物品 + 时间段 → 不是 UP」规则。
/// </summary>
public class GachaNoUpTests
{
    [Theory]
    [InlineData("genshin", 301, 10000016, false)]  // 迪卢克：角色活动池常驻
    [InlineData("genshin", 400, 10000016, false)]  // 400 归一成 301 后同样命中
    [InlineData("genshin", 301, 10000089, true)]   // 芙宁娜：限定池角色
    [InlineData("starrail", 11, 1101, false)]      // 布洛妮娅：角色活动跃迁常驻
    [InlineData("starrail", 11, 999999, true)]     // 表里没有的 5★ → 默认 UP
    public void ComputeIsUp_UsesPoolNoUpTable(string game, int type, int itemId, bool expected)
    {
        Assert.Equal(expected, GachaNoUp.ComputeIsUp(game, type, itemId, new DateTime(2026, 1, 1)));
    }

    [Theory]
    [InlineData("genshin", 200)]   // 常驻祈愿：没有不 UP 的物品
    [InlineData("genshin", 100)]   // 新手祈愿
    [InlineData("starrail", 1)]    // 常驻跃迁在表里没有（只有 11/12/21/22）
    public void ComputeIsUp_ReturnsNull_WhenPoolHasNoTable(string game, int type)
    {
        Assert.Null(GachaNoUp.ComputeIsUp(game, type, 10000002, new DateTime(2026, 1, 1)));
    }

    [Fact]
    public void ComputeIsUp_RespectsTimeWindows()
    {
        // 刻晴：2020-09-01..2021-02-17 常驻（不 UP）→ 2021-03-02 之后再次常驻；
        // 中间 2021-02-17 18:00..2021-03-02 16:00 的限定窗口是 UP。
        Assert.False(GachaNoUp.ComputeIsUp("genshin", 301, 10000042, new DateTime(2020, 12, 1)));
        Assert.True(GachaNoUp.ComputeIsUp("genshin", 301, 10000042, new DateTime(2021, 2, 20)));
        Assert.False(GachaNoUp.ComputeIsUp("genshin", 301, 10000042, new DateTime(2024, 12, 1)));
    }

    [Fact]
    public void ApplyUpFlags_RecomputesRank5Only()
    {
        static GachaRecord Record(int id, int rank, int itemId, bool isUp) => new()
        {
            Id = id,
            Name = "x",
            ItemType = "角色",
            RankType = rank,
            GachaType = 301,
            GachaTypeName = "角色活动祈愿",
            Time = new DateTime(2026, 1, 1),
            Uid = 100000001,
            IsUp = isUp,
            ItemId = itemId,
        };

        var records = new List<GachaRecord>
        {
            Record(1, 5, 10000016, true),   // 库里标了 UP，但迪卢克是常驻 → 重算成 false
            Record(2, 5, 10000089, false),  // 芙宁娜 → true
            Record(3, 4, 10000016, true),   // 4★ 不动
        };

        var result = GachaService.ApplyUpFlags("genshin", records);

        Assert.False(result[0].IsUp);
        Assert.True(result[1].IsUp);
        Assert.True(result[2].IsUp);
    }
}
