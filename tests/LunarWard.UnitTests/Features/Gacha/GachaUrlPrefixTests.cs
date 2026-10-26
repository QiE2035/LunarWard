using LunarWard.Core.Gacha.Genshin;
using LunarWard.Core.Gacha.StarRail;
using LunarWard.Core.Gacha.ZZZ;
using LunarWard.Core.Localization;
using Xunit;

namespace LunarWard.UnitTests.Features.Gacha;

/// <summary>
/// 抽卡链接解析回归：上游把三种来源的 URL 统一换算成抽卡接口前缀（<c>GachaLogClient.GetGachaUrlPrefix</c>）。
/// 这里通过派生类暴露 <c>protected</c> 成员，断言与上游逐字一致的换算规则：
/// webstatic（国服/国际服）、gs.hoyoverse.com（国际服）、public-operation（已带查询参数）、lang 归一化、非法 URL 抛错。
/// </summary>
public class GachaUrlPrefixTests
{
    private sealed class ProbedGenshinClient : GenshinGachaClient
    {
        public string Prefix(string url, string? lang = null) => GetGachaUrlPrefix(url, lang);
    }

    private sealed class ProbedStarRailClient : StarRailGachaClient
    {
        public string Prefix(string url, string? lang = null) => GetGachaUrlPrefix(url, lang);
    }

    private sealed class ProbedZZZClient : ZZZGachaClient
    {
        public string Prefix(string url, string? lang = null) => GetGachaUrlPrefix(url, lang);
    }

    private const string GiCn = "https://public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog";
    private const string GiOs = "https://public-operation-hk4e-sg.hoyoverse.com/gacha_info/api/getGachaLog";
    private const string SrCn = "https://public-operation-hkrpg.mihoyo.com/common/hkrpg_gacha_record/api/getGachaLog";
    private const string SrOs = "https://public-operation-hkrpg-sg.hoyoverse.com/common/hkrpg_gacha_record/api/getGachaLog";
    private const string ZzzCn = "https://public-operation-nap.mihoyo.com/common/gacha_record/api/getGachaLog";
    private const string ZzzOs = "https://public-operation-nap-sg.hoyoverse.com/common/gacha_record/api/getGachaLog";


    [Fact]
    public void Genshin_WebstaticCn_MapsToMainlandApiPrefix()
    {
        const string url = "https://webstatic.mihoyo.com/hk4e/event/e20190909gacha-v2/index.html?authkey=abc&lang=zh-cn#/log";
        string prefix = new ProbedGenshinClient().Prefix(url);
        Assert.StartsWith(GiCn, prefix);
        Assert.Contains("authkey=abc", prefix);
        Assert.DoesNotContain("#/log", prefix);
    }


    [Fact]
    public void Genshin_WebstaticGlobal_MapsToOverseasApiPrefix()
    {
        const string url = "https://gs.hoyoverse.com/genshin/event/e20190909gacha-v2/index.html?authkey=abc&lang=en-us#/log";
        string prefix = new ProbedGenshinClient().Prefix(url);
        Assert.StartsWith(GiOs, prefix);
        Assert.DoesNotContain("#/log", prefix);
    }


    [Fact]
    public void Genshin_PublicOperationUrl_StripsQueryParameters()
    {
        const string url = GiCn + "?authkey=abc&game_biz=hk4e_cn&region=cn_gf01&gacha_type=301&page=1&size=20&end_id=0&lang=zh-cn";
        string prefix = new ProbedGenshinClient().Prefix(url);
        Assert.StartsWith(GiCn, prefix);
        Assert.Contains("authkey=abc", prefix);
        Assert.DoesNotContain("&page=", prefix);
        Assert.DoesNotContain("&size=", prefix);
        Assert.DoesNotContain("&end_id=", prefix);
        Assert.DoesNotContain("&gacha_type=", prefix);
    }


    [Theory]
    [InlineData("ja", "ja-jp")]
    [InlineData("ko", "ko-kr")]
    [InlineData("zh-tw", "zh-tw")]
    [InlineData("en", "en-us")]
    public void Genshin_Lang_IsNormalizedByLanguageUtil(string raw, string expected)
    {
        const string url = GiCn + "?authkey=abc&lang=zh-cn&gacha_type=301&page=1";
        string prefix = new ProbedGenshinClient().Prefix(url, raw);
        Assert.Contains($"?authkey=abc&lang={expected}", prefix);
        Assert.DoesNotContain("lang=zh-cn", prefix);
    }


    [Fact]
    public void StarRail_WebstaticCn_MapsToMainlandApiPrefix()
    {
        const string url = "https://webstatic.mihoyo.com/hkrpg/event/e20211215gacha-v2/index.html?authkey=sr&lang=zh-cn#/log";
        string prefix = new ProbedStarRailClient().Prefix(url);
        Assert.StartsWith(SrCn, prefix);
        Assert.Contains("authkey=sr", prefix);
        // 上游 StarRail 分支不剥 `#/log`（auth 直接取 `?` 之后，fragment 不影响接口），此处锁定该行为
        Assert.Contains("#/log", prefix);
    }


    [Fact]
    public void StarRail_WebstaticGlobal_MapsToOverseasApiPrefix()
    {
        const string url = "https://gs.hoyoverse.com/hkrpg/event/e20211215gacha-v2/index.html?authkey=sr&lang=en-us#/log";
        string prefix = new ProbedStarRailClient().Prefix(url);
        Assert.StartsWith(SrOs, prefix);
    }


    [Fact]
    public void StarRail_PublicOperationUrl_StripsQueryParameters()
    {
        string url = SrCn + "?authkey=sr&game_biz=hkrpg_cn&gacha_type=11&page=1&size=20&end_id=0&lang=zh-cn";
        string prefix = new ProbedStarRailClient().Prefix(url);
        Assert.StartsWith(SrCn, prefix);
        Assert.DoesNotContain("&page=", prefix);
        Assert.DoesNotContain("&size=", prefix);
        Assert.DoesNotContain("&end_id=", prefix);
        Assert.DoesNotContain("&gacha_type=", prefix);
    }


    [Fact]
    public void ZZZ_WebstaticCn_MapsToMainlandApiPrefix()
    {
        const string url = "https://webstatic.mihoyo.com/nap/event/e20230424gacha-v2/index.html?authkey=zzz&lang=zh-cn#/log";
        string prefix = new ProbedZZZClient().Prefix(url);
        Assert.StartsWith(ZzzCn, prefix);
        Assert.Contains("authkey=zzz", prefix);
        Assert.DoesNotContain("#/log", prefix);
    }


    [Fact]
    public void ZZZ_WebstaticGlobal_MapsToOverseasApiPrefix()
    {
        const string url = "https://gs.hoyoverse.com/nap/event/e20230424gacha-v2/index.html?authkey=zzz&lang=en-us#/log";
        string prefix = new ProbedZZZClient().Prefix(url);
        Assert.StartsWith(ZzzOs, prefix);
    }


    [Fact]
    public void ZZZ_PublicOperationUrl_StripsRealGachaTypeAsWell()
    {
        string url = ZzzCn + "?authkey=zzz&game_biz=nap_cn&real_gacha_type=2&page=1&size=20&end_id=0&lang=zh-cn";
        string prefix = new ProbedZZZClient().Prefix(url);
        Assert.StartsWith(ZzzCn, prefix);
        Assert.DoesNotContain("&real_gacha_type=", prefix);
        Assert.DoesNotContain("&page=", prefix);
        Assert.DoesNotContain("&end_id=", prefix);
    }


    [Fact]
    public void Genshin_UnknownUrl_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ProbedGenshinClient().Prefix("https://example.com/not-a-gacha-url"));
        Assert.Equal(CoreLang.Gacha_CannotParseTheWishRecordURL, ex.Message);
    }


    [Fact]
    public void StarRail_UnknownUrl_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ProbedStarRailClient().Prefix("not a url"));
        Assert.Equal(CoreLang.Gacha_CannotParseTheWarpRecordURL, ex.Message);
    }


    [Fact]
    public void ZZZ_UnknownUrl_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ProbedZZZClient().Prefix("not a url"));
        Assert.Equal(CoreLang.Gacha_CannotParseTheWishRecordURL, ex.Message);
    }
}
