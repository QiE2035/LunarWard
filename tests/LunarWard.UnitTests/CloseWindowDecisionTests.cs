using LunarWard.Services;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 关闭窗口选项的判定：上游 <c>AppConfig.CloseWindowOption</c> 落盘只有 Hide/Exit，
/// 0（未设置）表示「每次询问」（LunarWard 用 <c>Ask</c> 显式表达，也是默认值）；
/// 托盘不可用时 Hide 必须退化成 Exit，不能静默隐藏。
/// </summary>
public class CloseWindowDecisionTests
{
    [Theory]
    [InlineData("Hide")]
    [InlineData("hide")]
    [InlineData("HIDE")]
    public void Parse_Hide_IsCaseInsensitive(string raw)
    {
        Assert.Equal(CloseWindowAction.Hide, CloseWindowDecision.Parse(raw));
    }


    [Theory]
    [InlineData("Exit")]
    [InlineData("EXIT")]
    public void Parse_Exit_IsCaseInsensitive(string raw)
    {
        Assert.Equal(CloseWindowAction.Exit, CloseWindowDecision.Parse(raw));
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ask")]
    [InlineData("something-else")]
    public void Parse_UnsetOrUnknown_IsAsk(string? raw)
    {
        Assert.Equal(CloseWindowAction.Ask, CloseWindowDecision.Parse(raw));
    }


    [Theory]
    [InlineData("Hide", true, CloseWindowAction.Hide)]
    [InlineData("Hide", false, CloseWindowAction.Exit)]
    [InlineData("Exit", true, CloseWindowAction.Exit)]
    [InlineData("Exit", false, CloseWindowAction.Exit)]
    [InlineData(null, true, CloseWindowAction.Ask)]
    [InlineData(null, false, CloseWindowAction.Ask)]
    [InlineData("", false, CloseWindowAction.Ask)]
    public void Resolve_AppliesTrayAvailability(string? option, bool trayAvailable, CloseWindowAction expected)
    {
        Assert.Equal(expected, CloseWindowDecision.Resolve(option, trayAvailable));
    }


    /// <summary>托盘不可用时不能把 Hide 当成可用选项亮给用户（否则关窗后窗口就没了）。</summary>
    [Fact]
    public void CanOfferHide_FollowsTrayAvailability()
    {
        Assert.True(CloseWindowDecision.CanOfferHide(trayAvailable: true));
        Assert.False(CloseWindowDecision.CanOfferHide(trayAvailable: false));
    }


    /// <summary>写回配置用的字符串必须是 Resolve 能还原回同一个动作的字面量。</summary>
    [Fact]
    public void OptionLiterals_RoundTrip()
    {
        Assert.Equal(CloseWindowAction.Hide, CloseWindowDecision.Parse(CloseWindowDecision.HideOption));
        Assert.Equal(CloseWindowAction.Exit, CloseWindowDecision.Parse(CloseWindowDecision.ExitOption));
        Assert.Equal(CloseWindowAction.Ask, CloseWindowDecision.Parse(CloseWindowDecision.AskOption));
    }
}
