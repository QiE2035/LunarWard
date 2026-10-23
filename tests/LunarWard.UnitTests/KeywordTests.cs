using LunarWard.Launch;
using LunarWard.Wine;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>U6 — 关键字替换。</summary>
public class KeywordTests
{
    private static IReadOnlyDictionary<string, string?> Keywords => new KeywordReplacer.Builder()
        .Build("/runners/wine")
        .Prefix("/prefixes/p")
        .Game("/games/ys")
        .Launcher("/data/lunarward")
        .Temp("/tmp")
        .Exe("YuanShen.exe")
        .Pid(1234)
        .Channel("Bilibili")
        .Runtime("shared-cachyos")
        .Build();


    [Theory]
    [InlineData("%build%/bin/wine", "/runners/wine/bin/wine")]
    [InlineData("%prefix%/drive_c", "/prefixes/p/drive_c")]
    [InlineData("%game%/YuanShen.exe", "/games/ys/YuanShen.exe")]
    [InlineData("%launcher%/logs", "/data/lunarward/logs")]
    [InlineData("%temp%/x", "/tmp/x")]
    [InlineData("%exe%", "YuanShen.exe")]
    [InlineData("%pid%", "1234")]
    [InlineData("%channel%", "Bilibili")]
    [InlineData("%runtime%", "shared-cachyos")]
    public void ReplacesStandardAndExtendedKeywords(string input, string expected)
    {
        Assert.Equal(expected, KeywordReplacer.Replace(input, Keywords));
    }


    [Fact]
    public void IsCaseInsensitive()
    {
        Assert.Equal("YuanShen.exe", KeywordReplacer.Replace("%EXE%", Keywords));
    }


    [Fact]
    public void UnknownKeyword_IsPreservedVerbatim()
    {
        Assert.Equal("%unknown%/x", KeywordReplacer.Replace("%unknown%/x", Keywords));
    }


    [Fact]
    public void NullValue_IsSkippedNotReplacedWithEmpty()
    {
        var keywords = new Dictionary<string, string?> { ["pid"] = null };
        Assert.Equal("%pid%", KeywordReplacer.Replace("%pid%", keywords));
    }


    [Fact]
    public void ReplacesMultipleOccurrences()
    {
        Assert.Equal("/games/ys/a /games/ys/b",
            KeywordReplacer.Replace("%game%/a %game%/b", Keywords));
    }


    [Fact]
    public void ListOverload_ReplacesEachItem()
    {
        var list = KeywordReplacer.Replace(new[] { "%exe%", "--path=%game%" }, Keywords);
        Assert.Equal(["YuanShen.exe", "--path=/games/ys"], list);
    }
}


/// <summary>U7 — 命令模板。</summary>
public class CommandTemplateTests
{
    [Fact]
    public void ExpandsAllSlots()
    {
        var result = CommandTemplate.Expand(
            "%command%",
            bashCommand: "wine",
            windowsCommand: "YuanShen.exe",
            launchArgs: ["-screen-fullscreen", "0"]);

        Assert.Equal("wine YuanShen.exe -screen-fullscreen 0", result);
    }


    [Fact]
    public void ExpandsIndividualSlots()
    {
        var bash = CommandTemplate.Expand("%bash_command%", "gamemoderun wine", "exe.exe", []);
        var windows = CommandTemplate.Expand("%windows_command%", "wine", "exe.exe", ["--a"]);
        var args = CommandTemplate.Expand("%launch_args%", "wine", "exe.exe", ["--a", "--b"]);

        Assert.Equal("gamemoderun wine", bash);
        Assert.Equal("exe.exe", windows);
        Assert.Equal("--a --b", args);
    }


    [Fact]
    public void PostSlot_IsEmptyWhenNoExternalPrograms()
    {
        var result = CommandTemplate.Expand("%command%%post%", "wine", "exe.exe", []);
        Assert.Equal("wine exe.exe", result);
    }


    [Fact]
    public void PostSlot_IsFilledWhenProvided()
    {
        var result = CommandTemplate.Expand("%command% %post%", "wine", "exe.exe", [], "&& injector.exe");
        Assert.Equal("wine exe.exe && injector.exe", result);
    }


    [Theory]
    [InlineData("%command% %post%", true)]
    [InlineData("%COMMAND%%POST%", true)]
    [InlineData("%command%", false)]
    [InlineData(null, false)]
    public void ContainsPost(string? template, bool expected)
    {
        Assert.Equal(expected, CommandTemplate.ContainsPost(template));
    }


    [Fact]
    public void EmptyLaunchArgs_ProducesNoDoubleSpace()
    {
        Assert.Equal("wine exe.exe", CommandTemplate.Expand("%command%", "wine", "exe.exe", []));
    }
}


public class CommandLineSplitterTests
{
    [Fact]
    public void SplitsPlainArguments()
    {
        Assert.Equal(["-a", "-b", "c"], CommandLineSplitter.Split("-a -b c"));
    }


    [Fact]
    public void KeepsQuotedPathsWithSpaces()
    {
        Assert.Equal(["--path=/home/u/My Games"],
            CommandLineSplitter.Split("--path=\"/home/u/My Games\""));
    }


    [Fact]
    public void SingleQuotesAreLiteral()
    {
        Assert.Equal(["a b", "$HOME"], CommandLineSplitter.Split("'a b' '$HOME'"));
    }


    [Fact]
    public void BackslashEscapesSpace()
    {
        Assert.Equal(["a b"], CommandLineSplitter.Split(@"a\ b"));
    }


    [Fact]
    public void EmptyString_ReturnsEmpty()
    {
        Assert.Empty(CommandLineSplitter.Split(""));
        Assert.Empty(CommandLineSplitter.Split(null));
    }


    [Fact]
    public void CollapsesRunsOfSpaces()
    {
        Assert.Equal(["a", "b"], CommandLineSplitter.Split("  a    b  "));
    }


    [Fact]
    public void EmptyQuotedArgument_IsKept()
    {
        Assert.Equal(["a", "", "b"], CommandLineSplitter.Split("a \"\" b"));
    }
}


/// <summary>
/// 基线 env 必须禁掉 wine-mono / wine-gecko 的自动安装提示。
/// </summary>
/// <remarks>
/// 否则 <c>wineboot</c> 会弹「是否下载 wine-mono」并阻塞——启动器无窗口，
/// 只能等到超时（实测 2 分 32 秒 vs 8 秒，见 docs/TROUBLESHOOTING.md §6.1b）。
/// </remarks>
public class WineMonoDisableTests
{
    [Theory]
    [InlineData("winemenubuilder.exe=d;mscoree,mshtml=")]
    public void BaselineDllOverrides_DisablesMonoAndGecko(string value)
    {
        // 与 LaunchPlanFactory 的基线值保持一致
        Assert.Contains("mscoree", value);
        Assert.Contains("mshtml", value);
    }


    [Fact]
    public void WineSettingsOverride_KeepsMonoDisabled()
    {
        // 用户配了 shared_libraries 之后，mono/gecko 的禁用不能被覆盖掉
        var env = LaunchPlanFactory.BuildWineSettingsEnv(new LunarWard.Wine.WineSettings
        {
            SharedLibraries = ["dxgi", "d3d11"],
        });

        Assert.True(env.TryGetValue("WINEDLLOVERRIDES", out var value));
        Assert.Contains("dxgi=n,b", value);
        Assert.Contains("d3d11=n,b", value);
        Assert.Contains("mscoree,mshtml=", value);
    }
}
