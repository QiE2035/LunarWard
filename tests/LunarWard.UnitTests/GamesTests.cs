using LunarWard.Games;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>U1 — <c>config.ini</c> 四渠道往返，且逐字节等于游戏原生格式。</summary>
public class ConfigIniRoundTripTests
{
    /// <summary>
    /// 三个真实样本，逐字节取自本机游戏目录（<c>~/games/hk4e_cn/config.ini</c> 与
    /// <c>~/games/nap_cn/config.ini</c>）。注意：<b>无结尾换行</b>。
    /// </summary>
    public const string GenshinBilibiliNative = "[General]\nchannel=14\ncps=bilibili\ngame_version=7.1.0\nsub_channel=0";

    public const string ZzzOfficialNative = "[General]\nchannel=1\ncps=mihoyo\ngame_version=3.3.0\nsub_channel=0";

    public const string DocSample = "[General]\nchannel=14\ncps=bilibili\ngame_version=5.0.0\nsub_channel=0";


    [Fact]
    public void DocSample_IsByteIdentical()
    {
        // docs/03 与 anime-launcher-sdk 单测里的原始样例
        Assert.Equal(DocSample, GameConfigIniFile.Parse(DocSample).Generate());
    }


    [Fact]
    public void RealGenshinBilibili_IsByteIdentical()
    {
        var doc = GameConfigIniFile.Parse(GenshinBilibiliNative);
        Assert.Equal(GenshinBilibiliNative, doc.Generate());
        Assert.Equal(14, doc.General.Channel);
        Assert.Equal("bilibili", doc.General.Cps);
        Assert.Equal(0, doc.General.SubChannel);
        Assert.Equal(new Version(7, 1, 0), doc.General.GameVersion);
    }


    [Fact]
    public void RealZzzOfficial_IsByteIdentical()
    {
        var doc = GameConfigIniFile.Parse(ZzzOfficialNative);
        Assert.Equal(ZzzOfficialNative, doc.Generate());
        Assert.Equal(1, doc.General.Channel);
        Assert.Equal("mihoyo", doc.General.Cps);
    }


    [Fact]
    public void NoTrailingNewline()
    {
        var text = GameConfigIniFile.Parse(GenshinBilibiliNative).Generate();
        Assert.False(text.EndsWith('\n'));
        Assert.Equal("\n", GameConfigIniFile.LineBreak);
    }


    [Theory]
    [InlineData(GameChannel.Official, 1, "mihoyo", 0)]
    [InlineData(GameChannel.Bilibili, 14, "bilibili", 0)]
    public void FourChannels_RoundTrip(GameChannel channel, int channelId, string cps, int subChannel)
    {
        var profile = ChannelCatalog.Get(channel);
        Assert.Equal(channelId, profile.ChannelId);
        Assert.Equal(cps, profile.Cps);
        Assert.Equal(subChannel, profile.SubChannel);

        var doc = GameConfigIniFile.Create(profile, "5.0.0");
        var text = doc.Generate();
        Assert.Contains($"channel={channelId}", text);
        Assert.Contains($"cps={cps}", text);
        Assert.Contains($"sub_channel={subChannel}", text);

        var reparsed = GameConfigIniFile.Parse(text);
        Assert.Equal(channelId, reparsed.General.Channel);
        Assert.Equal(cps, reparsed.General.Cps);
        Assert.Equal(subChannel, reparsed.General.SubChannel);
        Assert.Equal(text, reparsed.Generate());
    }


    [Fact]
    public void EpicAndGooglePlay_DoNotLoseValues()
    {
        foreach (var (profile, channelId, cps, sub) in new[]
                 {
                     (ChannelProfile.Epic, 1, "pcseaepic", 3),
                     (ChannelProfile.GooglePlay, 1, "pcgoogle", 6),
                 })
        {
            var doc = GameConfigIniFile.Create(profile);
            var text = doc.Generate();
            Assert.Contains($"channel={channelId}", text);
            Assert.Contains($"cps={cps}", text);
            Assert.Contains($"sub_channel={sub}", text);
            Assert.Equal(text, GameConfigIniFile.Parse(text).Generate());
        }
    }


    [Fact]
    public void Parse_EmptyOrNull_DoesNotThrow()
    {
        Assert.NotNull(GameConfigIniFile.Parse(null));
        Assert.NotNull(GameConfigIniFile.Parse(""));
        Assert.NotNull(GameConfigIniFile.Parse("   \n\n"));
    }


    [Fact]
    public void Parse_ToleratesCrlfAndSpaces()
    {
        var doc = GameConfigIniFile.Parse("[General]\r\nchannel = 14\r\ncps = bilibili\r\n");
        Assert.Equal(14, doc.General.Channel);
        Assert.Equal("bilibili", doc.General.Cps);
    }
}


/// <summary>U2 — 未知键保留。</summary>
public class ConfigIniExtraKeysTests
{
    [Fact]
    public void UnknownKeys_SurviveReadModifyWrite()
    {
        const string original = "[General]\nchannel=1\ncps=mihoyo\ngame_version=7.0.0\n"
                                + "sub_channel=0\nwpf_version=1.2.3\npredownload=abc";

        var doc = GameConfigIniFile.Parse(original);
        Assert.True(doc.TryGetExtra("wpf_version", out var wpf));
        Assert.Equal("1.2.3", wpf);
        Assert.True(doc.TryGetExtra("predownload", out var pre));
        Assert.Equal("abc", pre);

        // 切到 B 站
        doc.General.Channel = 14;
        doc.General.Cps = "bilibili";

        var text = doc.Generate();
        Assert.Contains("wpf_version=1.2.3", text);
        Assert.Contains("predownload=abc", text);
        Assert.Contains("channel=14", text);

        // 二次往返幂等
        Assert.Equal(text, GameConfigIniFile.Parse(text).Generate());
    }


    [Fact]
    public void UnknownKeys_AreAppendedAfterKnownKeys()
    {
        var doc = GameConfigIniFile.Parse("[General]\nchannel=1\ncps=mihoyo\nextra_key=v");
        var text = doc.Generate();
        var lines = text.Split('\n');
        Assert.Equal("[General]", lines[0]);
        Assert.Equal("extra_key=v", lines[^1]);
    }


    [Fact]
    public void SetExtra_AddsAndUpdates()
    {
        var doc = GameConfigIniFile.Parse("[General]\nchannel=1");
        doc.SetExtra("new_key", "v1");
        Assert.Contains("new_key=v1", doc.Generate());
        doc.SetExtra("new_key", "v2");
        Assert.Contains("new_key=v2", doc.Generate());
        Assert.DoesNotContain("new_key=v1", doc.Generate());
    }


    [Fact]
    public void Write_IsAtomicAndReadable()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("config.ini");
        var doc = GameConfigIniFile.Parse("[General]\nchannel=14\ncps=bilibili");
        doc.Write(path);

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal(14, GameConfigIniFile.Read(path).General.Channel);
    }
}


/// <summary>U10 — B 站前置校验的四种组合。</summary>
public class ChannelValidationTests
{
    private static (GameDefinition Def, string Dir) Setup(bool china, bool hasDll, bool dllEmpty)
    {
        var temp = new TempDirectory();
        var def = china ? GameCatalog.GenshinChina : GameCatalog.GenshinGlobal;

        var plugins = Path.Join(temp.Path, def.DataDirectory, "Plugins");
        Directory.CreateDirectory(plugins);
        var dll = Path.Join(plugins, "PCGameSDK.dll");
        if (hasDll)
        {
            File.WriteAllBytes(dll, dllEmpty ? [] : [1, 2, 3, 4]);
        }

        File.WriteAllText(Path.Join(temp.Path, "YuanShen.exe"), "MZ");
        return (def, temp.Path);
    }


    [Fact]
    public void NonChina_IsHardRejected()
    {
        var (def, dir) = Setup(china: false, hasDll: true, dllEmpty: false);
        var result = ChannelValidator.Validate(GameChannel.Bilibili, def, dir);
        Assert.False(result.Ok);
        Assert.Contains("国服", result.Error);
    }


    [Fact]
    public void MissingDll_IsRejectedAndOffersDownload()
    {
        var (def, dir) = Setup(china: true, hasDll: false, dllEmpty: false);
        var result = ChannelValidator.Validate(GameChannel.Bilibili, def, dir);
        Assert.False(result.Ok);
        Assert.True(result.NeedsPluginDownload);
        Assert.Contains("PCGameSDK", result.Error);
    }


    [Fact]
    public void EmptyDll_IsRejectedAndOffersDownload()
    {
        var (def, dir) = Setup(china: true, hasDll: true, dllEmpty: true);
        var result = ChannelValidator.Validate(GameChannel.Bilibili, def, dir);
        Assert.False(result.Ok);
        Assert.True(result.NeedsPluginDownload);
    }


    [Fact]
    public void Normal_IsOk_ButNeedsConfigRewriteWhenIniMissing()
    {
        var (def, dir) = Setup(china: true, hasDll: true, dllEmpty: false);
        var result = ChannelValidator.Validate(GameChannel.Bilibili, def, dir);
        Assert.True(result.Ok);
        Assert.True(result.NeedsConfigRewrite);
        Assert.False(result.NeedsPluginDownload);
    }


    [Fact]
    public void Normal_WithCorrectConfig_IsFullyOk()
    {
        var (def, dir) = Setup(china: true, hasDll: true, dllEmpty: false);
        ChannelValidator.ApplyChannel(def, dir, GameChannel.Bilibili);

        var result = ChannelValidator.Validate(GameChannel.Bilibili, def, dir);
        Assert.True(result.Ok);
        Assert.False(result.NeedsConfigRewrite);

        // 真的写进了游戏原生格式
        var text = File.ReadAllText(Path.Join(dir, "config.ini"));
        Assert.Contains("channel=14", text);
        Assert.Contains("cps=bilibili", text);
    }


    [Fact]
    public void Zzz_DoesNotSupportBilibili()
    {
        var temp = new TempDirectory();
        var def = GameCatalog.ZzzChina;
        var result = ChannelValidator.Validate(GameChannel.Bilibili, def, temp.Path);
        Assert.False(result.Ok);
        Assert.Contains("没有 Bilibili", result.Error);
    }


    [Fact]
    public void OfficialChannel_NeedsNoPlugin()
    {
        var (def, dir) = Setup(china: true, hasDll: false, dllEmpty: false);
        var result = ChannelValidator.Validate(GameChannel.Official, def, dir);
        Assert.True(result.Ok);
        Assert.False(result.NeedsPluginDownload);
    }


    [Fact]
    public void DetectChannel_ReadsBilibili()
    {
        var (def, dir) = Setup(china: true, hasDll: true, dllEmpty: false);
        ChannelValidator.ApplyChannel(def, dir, GameChannel.Bilibili);
        Assert.Equal(GameChannel.Bilibili, ChannelValidator.DetectChannel(dir)?.Kind);
    }
}


/// <summary>U11 — zip 条目 GBK 匹配。</summary>
public class BilibiliPluginEntryTests
{
    [Theory]
    // GBK 的 "64位" 前缀是 ASCII '6' '4' + 0xce 0xbb
    [InlineData("64\uFFFD/PCGameSDK.dll", true)]           // 替换字符也能命中（前缀 64）
    [InlineData("64\ufffd\ufffd/PCGameSDK.dll", true)]
    [InlineData("32位/PCGameSDK.dll", false)]
    // 段必须"以 64 开头"，x64 不满足（上游规则如此，不能放宽）
    [InlineData("x64/PCGameSDK.dll", false)]
    [InlineData("PCGameSDK.dll", false)]
    [InlineData("64bit/other.dll", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPluginEntry_MatchesByArchPrefixAndSuffix(string? name, bool expected)
    {
        Assert.Equal(expected, BilibiliPluginDownloader.IsPluginEntry(name));
    }


    [Fact]
    public void IsPluginEntry_RealGbkBytesFromResearch()
    {
        // docs/01-RESEARCH.md §3.3：条目名是 `64\xce\xbb/PCGameSDK.dll`
        var gbk = System.Text.Encoding.Latin1.GetString([0x36, 0x34, 0xce, 0xbb])
                  + "/PCGameSDK.dll";
        Assert.True(BilibiliPluginDownloader.IsPluginEntry(gbk));
    }


    [Fact]
    public void IsPluginEntry_CaseInsensitiveSuffix()
    {
        Assert.True(BilibiliPluginDownloader.IsPluginEntry("64位/pcgamesdk.dll"));
    }


    [Fact]
    public void ConstantsMatchResearch()
    {
        Assert.Equal("/PCGameSDK.dll", BilibiliPluginDownloader.PluginEntrySuffix);
        Assert.Equal("64", BilibiliPluginDownloader.PluginEntryArchPrefix);
        Assert.Equal("PCGameSDK.dll", BilibiliPluginDownloader.PluginFileName);
        Assert.Contains("pkg.biligame.com", BilibiliPluginDownloader.PluginUri);
        Assert.Equal("https://open.biligame.com/", BilibiliPluginDownloader.Referer);
        Assert.Contains("Mozilla/5.0", BilibiliPluginDownloader.UserAgent);
    }
}


/// <summary>GameCatalog 的基本不变量。</summary>
public class GameCatalogTests
{
    [Fact]
    public void FourDefinitions()
    {
        Assert.Equal(4, GameCatalog.All.Count);
        Assert.NotNull(GameCatalog.Get("genshin", GameEdition.China));
        Assert.NotNull(GameCatalog.Get("genshin", GameEdition.Global));
        Assert.NotNull(GameCatalog.Get("zzz", GameEdition.China));
        Assert.NotNull(GameCatalog.Get("zzz", GameEdition.Global));
        Assert.Null(GameCatalog.Get("hsr", GameEdition.China));
    }


    [Fact]
    public void GenshinChina_UsesYuanShenExeAndSupportsBilibili()
    {
        var def = GameCatalog.GenshinChina;
        Assert.Equal("YuanShen.exe", def.Executable);
        Assert.Equal("YuanShen_Data", def.DataDirectory);
        Assert.Equal("hk4e_cn", def.GameBiz);
        Assert.True(def.Supports(GameChannel.Bilibili));
    }


    [Fact]
    public void GenshinGlobal_DoesNotSupportBilibili()
    {
        Assert.False(GameCatalog.GenshinGlobal.Supports(GameChannel.Bilibili));
    }


    [Fact]
    public void Zzz_HasNoBilibiliChannel()
    {
        Assert.False(GameCatalog.ZzzChina.Supports(GameChannel.Bilibili));
        Assert.False(GameCatalog.ZzzGlobal.Supports(GameChannel.Bilibili));
    }


    [Fact]
    public void Detect_FromExecutableName()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.Combine("YuanShen.exe"), "MZ");
        Assert.Equal("genshin", GameCatalog.Detect(temp.Path)?.Kind);
        Assert.Equal(GameEdition.China, GameCatalog.Detect(temp.Path)?.Edition);
    }


    [Fact]
    public void FromGameBiz_MapsAllFour()
    {
        Assert.Equal("genshin", GameCatalog.FromGameBiz(new LunarWard.Core.GameBiz("hk4e_cn"))?.Kind);
        Assert.Equal("genshin", GameCatalog.FromGameBiz(new LunarWard.Core.GameBiz("hk4e_global"))?.Kind);
        Assert.Equal("zzz", GameCatalog.FromGameBiz(new LunarWard.Core.GameBiz("nap_cn"))?.Kind);
        Assert.Equal("zzz", GameCatalog.FromGameBiz(new LunarWard.Core.GameBiz("nap_global"))?.Kind);
    }
}


/// <summary>层 8 的游戏特化 env（含 <c>Override("")</c> 语义）。</summary>
public class GameAdapterTests
{
    [Fact]
    public void WineWayland_SetsDisplayToEmptyString_NotNull()
    {
        var result = GameAdapter.BuildEnv("genshin", wineWayland: true, timeoutFix: false,
            borderless: false, dx12: false, fsr: false);

        Assert.True(result.Env.ContainsKey("DISPLAY"));
        Assert.Equal("", result.Env["DISPLAY"]);
    }


    [Fact]
    public void WithoutWineWayland_DisplayIsNotTouched()
    {
        var result = GameAdapter.BuildEnv("genshin", wineWayland: false, timeoutFix: false,
            borderless: false, dx12: false, fsr: false);
        Assert.False(result.Env.ContainsKey("DISPLAY"));
    }


    [Fact]
    public void TimeoutFix_IsSetOnlyWhenEnabled()
    {
        var withFix = GameAdapter.BuildEnv("genshin", false, true, false, false, false);
        Assert.Equal("1", withFix.Env["WINE_ENABLE_TIMEOUT_FIX"]);

        var without = GameAdapter.BuildEnv("genshin", false, false, false, false, false);
        Assert.False(without.Env.ContainsKey("WINE_ENABLE_TIMEOUT_FIX"));
    }


    [Fact]
    public void Dx12_OnlyAppliesToZzz()
    {
        var zzz = GameAdapter.BuildEnv("zzz", false, false, false, dx12: true, fsr: false);
        Assert.Equal("1", zzz.Env["DXVK_ENABLE_NVAPI"]);

        var genshin = GameAdapter.BuildEnv("genshin", false, false, false, dx12: true, fsr: false);
        Assert.False(genshin.Env.ContainsKey("DXVK_ENABLE_NVAPI"));
    }


    [Fact]
    public void Fsr_SetsStrength()
    {
        var result = GameAdapter.BuildEnv("genshin", false, false, false, false, fsr: true);
        Assert.Equal("1", result.Env["WINE_FULLSCREEN_FSR"]);
        Assert.Equal("2", result.Env["WINE_FULLSCREEN_FSR_STRENGTH"]);
    }


    [Fact]
    public void Borderless_AddsArguments()
    {
        var result = GameAdapter.BuildEnv("genshin", false, false, borderless: true, false, false);
        Assert.NotEmpty(result.ExtraArguments);
        Assert.Contains("-screen-fullscreen", result.ExtraArguments);
    }


    [Fact]
    public void ApplyTo_WritesEnvLayerSource()
    {
        var builder = new LunarWard.Wine.EnvironmentBuilder();
        builder.Set(LunarWard.Wine.EnvLayer.Baseline, "A", "1");
        GameAdapter.BuildEnv("genshin", wineWayland: true, false, false, false, false).ApplyTo(builder);

        Assert.Equal("1", builder.Values["A"]);
        Assert.Equal("", builder.Values["DISPLAY"]);
        Assert.Equal(LunarWard.Wine.EnvLayer.GameAdapter, builder.Sources["DISPLAY"]);
    }
}
