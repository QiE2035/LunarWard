using LunarWard.Core;
using LunarWard.Core.GameNotice;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// <see cref="GameNoticeClient.GetGameNoticeUrl"/>：启动器铃铛打开的公告页地址
/// （Starward <c>GameNoticeWindow</c> 的 URL 生成）。
/// </summary>
public sealed class NoticeUrlTests
{
    [Theory]
    [InlineData("hk4e_cn", "bundle_id=hk4e_cn", "region=cn_gf01")]
    [InlineData("hk4e_global", "bundle_id=hk4e_global", "region=os_asia")]
    [InlineData("hkrpg_cn", "bundle_id=hkrpg_cn", "region=prod_gf_cn")]
    [InlineData("nap_cn", "bundle_id=nap_cn", "version=2.33")]
    [InlineData("nap_global", "bundle_id=nap_global", "region=prod_gf_jp")]
    public void Url_contains_biz_specific_parameters(string biz, string expected1, string expected2)
    {
        var url = GameNoticeClient.GetGameNoticeUrl(new GameBiz(biz), 123456789, "zh-CN");
        Assert.Contains(expected1, url);
        Assert.Contains(expected2, url);
        Assert.Contains("uid=123456789", url);
        Assert.Contains("lang=zh-cn", url);
    }


    [Fact]
    public void Unknown_uid_falls_back_to_placeholder()
    {
        var url = GameNoticeClient.GetGameNoticeUrl(new GameBiz("hk4e_cn"), 0);
        Assert.Contains("uid=100000000", url);
    }


    [Fact]
    public void English_language_is_filtered()
    {
        var url = GameNoticeClient.GetGameNoticeUrl(new GameBiz("hk4e_cn"), 1, "en-US");
        Assert.Contains("lang=en-us", url);
    }


    [Fact]
    public void Unknown_biz_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GameNoticeClient.GetGameNoticeUrl(new GameBiz("bh3_os"), 1));
    }
}
