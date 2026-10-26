using LunarWard.Features.GameLauncher;
using LunarWard.Games;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LunarWard.UnitTests.Features.GameLauncher;

/// <summary>
/// 上游 <c>Starward/src/Starward/Features/GameLauncher/GameConfigIni.cs</c>（40 行的
/// <c>[ConfigurationKeyName]</c> POCO）与本地 <c>LunarWard.Games.GameConfigIniFile</c>（config.ini 读写）。
/// 锁住两件事：键名映射与上游逐字一致；写出结果对同一输入逐字节稳定。
/// </summary>
public sealed class GameConfigIniTests
{
    private const string Canonical = """
        [General]
        channel=1
        cps=hyp_mihoyo
        game_version=5.2.0
        sub_channel=1
        sdk_version=5.2.0
        game_biz=hk4e_cn
        """;


    [Fact]
    public void PocoBindsAllUpstreamConfigurationKeys()
    {
        string path = Path.Join(Path.GetTempPath(), "lunarward-ini-" + Guid.NewGuid().ToString("N") + ".ini");
        File.WriteAllText(path, """
            [General]
            channel=1
            sub_channel=1
            cps=hyp_mihoyo
            game_version=5.2.0
            sdk_version=5.2.0
            game_biz=hk4e_cn
            uapc={"hk4e_cn":{"uapc":"x"}}
            downloading_mode=sophon
            """);

        try
        {
            var ini = new ConfigurationBuilder().AddIniFile(path).Build().Get<GameConfigIni>();

            Assert.NotNull(ini);
            Assert.Equal("hyp_mihoyo", ini!.Cps);
            Assert.Equal("5.2.0", ini.GameVersion);
            Assert.Equal("5.2.0", ini.SdkVersion);
            Assert.Equal("hk4e_cn", ini.GameBiz);
            Assert.Equal("sophon", ini.DownloadingMode);
            Assert.Equal("{\"hk4e_cn\":{\"uapc\":\"x\"}}", ini.Uapc);
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public void ParseThenGenerateIsByteIdentical()
    {
        var document = GameConfigIniFile.Parse(Canonical);

        Assert.Equal(Canonical, document.Generate());
    }


    [Fact]
    public void GenerateIsIdempotent()
    {
        string once = GameConfigIniFile.Parse(Canonical).Generate();
        string twice = GameConfigIniFile.Parse(once).Generate();

        Assert.Equal(once, twice);
    }


    [Fact]
    public void PreservesUnknownKeysAndOtherSections()
    {
        const string source = """
            [General]
            channel=14
            cps=hyp_mihoyo
            custom_key=keepme
            [Other]
            foo=bar
            """;

        string generated = GameConfigIniFile.Parse(source).Generate();

        Assert.Contains("[General]", generated);
        Assert.Contains("custom_key=keepme", generated);
        Assert.Contains("[Other]", generated);
        Assert.Contains("foo=bar", generated);
        // 换行符固定为 \n，不随宿主平台变化（上游 Windows 写 CRLF，见 docs/14）。
        Assert.DoesNotContain("\r\n", generated);
    }


    [Fact]
    public void KnownKeysSurviveRoundTripAfterEdit()
    {
        var document = GameConfigIniFile.CreateEmpty();
        document.General.Channel = 14;
        document.General.SubChannel = 0;
        document.General.Cps = "hyp_mihoyo";
        document.General.GameBiz = "hk4e_bilibili";

        var reparsed = GameConfigIniFile.Parse(document.Generate());

        Assert.Equal(14, reparsed.General.Channel);
        Assert.Equal(0, reparsed.General.SubChannel);
        Assert.Equal("hyp_mihoyo", reparsed.General.Cps);
        Assert.Equal("hk4e_bilibili", reparsed.General.GameBiz);
    }
}
