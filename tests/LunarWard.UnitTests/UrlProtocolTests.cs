using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>URL 协议的解析（<c>docs/05-ROADMAP.md</c> §3.3 的 P1 项）。</summary>
public class UrlProtocolTests
{
    [Fact]
    public void NoLunarWardArgument_ReturnsNull()
    {
        Assert.Null(UrlProtocol.TryParse(["/usr/bin/lunarward", "--screenshot", "x.png"], out var error));
        Assert.Null(error);
    }


    [Theory]
    [InlineData("lunarward://launch?profile=genshin-cn", "launch", "genshin-cn")]
    [InlineData("lunarward://shell?profile=zzz-cn", "shell", "zzz-cn")]
    [InlineData("lunarward://gacha?profile=genshin-cn", "gacha", "genshin-cn")]
    [InlineData("LUNARWARD://LAUNCH?profile=a", "launch", "a")]
    public void ParsesKnownVerbs(string uri, string expectedVerb, string expectedProfile)
    {
        var command = UrlProtocol.TryParse([uri], out var error);
        Assert.NotNull(command);
        Assert.Null(error);
        Assert.Equal(expectedVerb, command!.Verb);
        Assert.Equal(expectedProfile, command.Get("profile"));
    }


    [Fact]
    public void OpenWithoutQueryWorks()
    {
        var command = UrlProtocol.TryParse(["lunarward://open"], out _);
        Assert.NotNull(command);
        Assert.Equal("open", command!.Verb);
        Assert.Null(command.Get("profile"));
    }


    [Fact]
    public void UnknownVerb_DegradesToOpenAndReports()
    {
        var command = UrlProtocol.TryParse(["lunarward://rm-rf?profile=x"], out var error);
        Assert.NotNull(command);
        Assert.Equal("open", command!.Verb);
        Assert.NotNull(error);
        Assert.Contains("未知命令", error);
    }


    [Fact]
    public void ProfileIsJustAKey_NeverAPath()
    {
        // URI 里塞路径不会被当成路径，只是当作 profile 键
        var command = UrlProtocol.TryParse(["lunarward://launch?profile=../../etc/passwd"], out _);
        Assert.NotNull(command);
        Assert.Equal("../../etc/passwd", command!.Get("profile"));
        // 解析层不做任何文件访问——这只证明它原样保留了值，由调用方去配置里查键
        Assert.DoesNotContain("..", command.Verb);
    }


    [Fact]
    public void QueryValuesAreUrlDecoded()
    {
        var command = UrlProtocol.TryParse(["lunarward://launch?profile=%E5%8E%9F%E7%A5%9E%20cn"], out _);
        Assert.NotNull(command);
        Assert.Equal("原神 cn", command!.Get("profile"));
    }


    [Fact]
    public void MultipleArguments_AreCollected()
    {
        var command = UrlProtocol.TryParse(["lunarward://launch?profile=a&uid=100000000&lang=zh-cn"], out _);
        Assert.NotNull(command);
        Assert.Equal("a", command!.Get("profile"));
        Assert.Equal("100000000", command.Get("uid"));
        Assert.Equal("zh-cn", command.Get("lang"));
    }


    [Fact]
    public void PicksTheUriAmongOtherArguments()
    {
        var command = UrlProtocol.TryParse(["--verbose", "lunarward://launch?profile=x", "--foo"], out _);
        Assert.NotNull(command);
        Assert.Equal("launch", command!.Verb);
    }


    [Fact]
    public void BareScheme_IsTreatedAsOpenWithError()
    {
        // "lunarward://" 是合法 URI（host 为空）——没有 verb，按 open 处理并标注
        var command = UrlProtocol.TryParse(["lunarward://"], out var error);
        Assert.NotNull(command);
        Assert.Equal("open", command!.Verb);
        Assert.NotNull(error);
    }


    [Theory]
    [InlineData("launch", "启动档案")]
    [InlineData("shell", "环境 shell")]
    [InlineData("gacha", "抽卡记录")]
    [InlineData("open", "显示主窗口")]
    public void Describe_IsHumanReadable(string verb, string expectedFragment)
    {
        var command = new UrlProtocolCommand(verb, new Dictionary<string, string> { ["profile"] = "p" });
        Assert.Contains(expectedFragment, UrlProtocol.Describe(command));
    }


    [Fact]
    public void DesktopEntryValidation()
    {
        var good = """
        [Desktop Entry]
        Type=Application
        Exec=lunarward %U
        MimeType=x-scheme-handler/lunarward;
        """;
        Assert.True(UrlProtocol.IsDesktopEntryValid(good));

        Assert.False(UrlProtocol.IsDesktopEntryValid("Exec=lunarward %U\nMimeType=x-scheme-handler/other;"));
        Assert.False(UrlProtocol.IsDesktopEntryValid("Exec=lunarward\nMimeType=x-scheme-handler/lunarward;"));
    }


    [Fact]
    public void MimeType_MatchesPackagingFile()
    {
        // 协议名与本仓库的 .desktop 必须一致，否则点了链接不会唤起应用
        Assert.Equal("x-scheme-handler/lunarward", UrlProtocol.MimeType);

        var desktop = Path.Join(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "packaging", "lunarward.desktop");
        if (File.Exists(desktop))
        {
            var content = File.ReadAllText(desktop);
            Assert.True(UrlProtocol.IsDesktopEntryValid(content), ".desktop 与本模块的 MimeType/Exec 不一致");
        }
    }


    [Fact]
    public void BuildDesktopExec_UsesFieldCode()
    {
        Assert.Equal("/usr/bin/lunarward %U", UrlProtocol.BuildDesktopExec("/usr/bin/lunarward"));
    }
}
