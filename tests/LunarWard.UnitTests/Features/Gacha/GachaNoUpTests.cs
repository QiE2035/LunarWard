using LunarWard.Core.Gacha;
using Xunit;

namespace LunarWard.UnitTests.Features.Gacha;

/// <summary>
/// 非 UP 区间回归：上游把「某个 5★ 物品在某段时间里不是 UP」写成 <c>GachaNoUp.Dictionary</c>，
/// 抽卡记录同步后用它给<b>历史补录</b>的 5★ 判 IsUp（接口本身不返回老数据）。
/// 这里校验：三个游戏都有区间表、键的拼法（<c>{GameBiz}{gacha_type}</c>）、
/// 以及 LunarWard 补充的 <c>ComputeIsUp</c> 对「区间内/区间外/未知物品/未知游戏/原神 400 归一到 301」的判定。
/// </summary>
public class GachaNoUpTests
{
    [Theory]
    [InlineData("hk4e301")]
    [InlineData("hkrpg11")]
    [InlineData("hkrpg12")]
    [InlineData("hkrpg21")]
    [InlineData("nap2")]
    public void Dictionary_ContainsExpectedKeys(string key)
    {
        Assert.True(GachaNoUp.Dictionary.ContainsKey(key), $"缺少非 UP 区间表：{key}");
        Assert.NotEmpty(GachaNoUp.Dictionary[key].Items);
        Assert.Equal(key, GachaNoUp.Dictionary[key].Game.Value + GachaNoUp.Dictionary[key].GachaType.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }


    [Fact]
    public void ComputeIsUp_Genshin_InsideNoUpRange_ReturnsFalse()
    {
        var (itemId, start, end) = FindFirstNoUpWindow("hk4e301");
        Assert.False(GachaNoUp.ComputeIsUp("genshin", 301, itemId, start.AddDays(1)));
        Assert.False(GachaNoUp.ComputeIsUp("genshin", 301, itemId, end.AddDays(-1)));
    }


    [Fact]
    public void ComputeIsUp_Genshin_OutsideNoUpRange_ReturnsTrue()
    {
        var (itemId, start, _) = FindFirstNoUpWindow("hk4e301");
        Assert.True(GachaNoUp.ComputeIsUp("genshin", 301, itemId, start.AddDays(-1)));
    }


    [Fact]
    public void ComputeIsUp_Genshin_UnknownItem_ReturnsTrue()
    {
        // 表里没有的物品视为 UP（与上游「默认是 UP」一致）
        Assert.True(GachaNoUp.ComputeIsUp("genshin", 301, int.MaxValue, new DateTime(2024, 1, 1)));
    }


    [Fact]
    public void ComputeIsUp_Genshin_UnknownGachaType_ReturnsNull()
    {
        Assert.Null(GachaNoUp.ComputeIsUp("genshin", 9999, int.MaxValue, new DateTime(2024, 1, 1)));
    }


    [Fact]
    public void ComputeIsUp_UnknownGame_ReturnsNull()
    {
        Assert.Null(GachaNoUp.ComputeIsUp("bh3", 301, 1, new DateTime(2024, 1, 1)));
    }


    [Fact]
    public void ComputeIsUp_Genshin_400_IsNormalizedTo301()
    {
        var (itemId, start, _) = FindFirstNoUpWindow("hk4e301");
        var time = start.AddDays(1);
        Assert.Equal(
            GachaNoUp.ComputeIsUp("genshin", 301, itemId, time),
            GachaNoUp.ComputeIsUp("genshin", 400, itemId, time));
    }


    [Fact]
    public void ComputeIsUp_StarRail_InsideNoUpRange_ReturnsFalse()
    {
        var (itemId, start, _) = FindFirstNoUpWindow("hkrpg11");
        Assert.False(GachaNoUp.ComputeIsUp("starrail", 11, itemId, start.AddDays(1)));
    }


    [Fact]
    public void ComputeIsUp_ZZZ_InsideNoUpRange_ReturnsFalse()
    {
        var key = GachaNoUp.Dictionary.Keys.First(x => x.StartsWith("nap", StringComparison.Ordinal) && HasNoUpWindow(x));
        var gachaType = GachaNoUp.Dictionary[key].GachaType;
        var (itemId, start, _) = FindFirstNoUpWindow(key);
        Assert.False(GachaNoUp.ComputeIsUp("zzz", gachaType, itemId, start.AddDays(1)));
    }


    private static bool HasNoUpWindow(string key) => GachaNoUp.Dictionary[key].Items.Values.Any(x => x.NoUpTimes.Count > 0);

    private static (int ItemId, DateTime Start, DateTime End) FindFirstNoUpWindow(string key)
    {
        foreach (var (itemId, item) in GachaNoUp.Dictionary[key].Items)
        {
            if (item.NoUpTimes.Count > 0)
            {
                return (itemId, item.NoUpTimes[0].Start, item.NoUpTimes[0].End);
            }
        }
        throw new InvalidOperationException($"{key} 没有非 UP 区间数据，测试前提失效");
    }
}
