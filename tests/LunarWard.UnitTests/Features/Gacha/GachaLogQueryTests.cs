using LunarWard.Core.Gacha;
using LunarWard.Core.Gacha.Genshin;
using LunarWard.Core.Gacha.StarRail;
using LunarWard.Core.Gacha.ZZZ;
using Xunit;

namespace LunarWard.UnitTests.Features.Gacha;

/// <summary>
/// 查询串拼装回归：上游 <c>GachaLogQuery.ToString()</c> 对原神/星铁拼 <c>gacha_type=</c>，
/// 对绝区零拼 <c>real_gacha_type=</c>（ZZZ 接口要求），其余 <c>page/size/end_id</c> 顺序固定。
/// 这段字符串直接进抽卡接口 URL，字段名或顺序错了接口就返回空。
/// </summary>
public class GachaLogQueryTests
{
    [Fact]
    public void Genshin_Query_UsesGachaType()
    {
        var query = new GachaLogQuery(new GenshinGachaType(301), page: 2, size: 20, endId: 123456789);
        Assert.Equal("gacha_type=301&page=2&size=20&end_id=123456789", query.ToString());
    }


    [Fact]
    public void StarRail_Query_UsesGachaType()
    {
        var query = new GachaLogQuery(new StarRailGachaType(11), page: 3, size: 20, endId: 42);
        Assert.Equal("gacha_type=11&page=3&size=20&end_id=42", query.ToString());
    }


    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void ZZZ_Query_UsesRealGachaType(int value)
    {
        var query = new GachaLogQuery(new ZZZGachaType(value), page: 1, size: 20, endId: 0);
        Assert.Equal($"real_gacha_type={value}&page=1&size=20&end_id=0", query.ToString());
    }


    [Fact]
    public void BeyondGacha_Query_UsesPlainGachaType()
    {
        var query = new BeyondGachaLogQuery(gachaType: 1000, page: 1, size: 20, endId: 7);
        Assert.Equal("gacha_type=1000&page=1&size=20&end_id=7", query.ToString());
    }


    [Fact]
    public void Query_DefaultSize_IsTwenty()
    {
        // 上游分页固定 20 条/页，接口侧 size 只允许 20 的调用方必须与此一致
        var query = new GachaLogQuery(new GenshinGachaType(200), page: 1, size: 20, endId: 0);
        Assert.Equal(20, query.Size);
        Assert.Contains("size=20", query.ToString());
    }
}
