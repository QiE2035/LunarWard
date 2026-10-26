using LunarWard.App.ViewModels.Starward;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 工具箱左栏战绩菜单按游戏区分（Starward <c>GameRecordPage.InitializeNavigationViewItemVisibility</c>）：
/// 原神只显示原神组、星铁只显示星铁组、绝区零只显示绝区零组，未知游戏留空。
/// </summary>
public sealed class ToolboxMenuTests
{
    [Fact]
    public void GenshinShowsOnlyGenshinEntries()
    {
        var entries = ToolboxPageViewModel.GetMenuEntries("genshin");

        Assert.Equal(
            ["BattleChronicle", "TravelersDiary", "SpiralAbyss", "ImaginariumTheater", "StygianOnslaught"],
            entries.Select(x => x.Tag));
        Assert.Equal(
            ["\uE9D9", "\uE787", "\uE8A1", "\uE7FC", "\uE9D2"],
            entries.Select(x => x.Icon));
    }


    [Fact]
    public void StarRailShowsOnlyStarRailEntries()
    {
        var entries = ToolboxPageViewModel.GetMenuEntries("starrail");

        Assert.Equal(
            ["TrailblazeCalendar", "SimulatedUniverse", "ForgottenHall", "PureFiction", "ApocalypticShadow", "ChallengePeak"],
            entries.Select(x => x.Tag));
    }


    [Fact]
    public void ZzzShowsOnlyZzzEntries()
    {
        var entries = ToolboxPageViewModel.GetMenuEntries("zzz");

        Assert.Equal(
            ["BattleChronicle", "InterKnotMonthlyReport", "ShiyuDefense", "DeadlyAssault"],
            entries.Select(x => x.Tag));
        // 星铁的菜单项绝不能出现在绝区零下（本轮 bug 的原形）。
        Assert.DoesNotContain(entries, x => x.Tag is "TrailblazeCalendar" or "SimulatedUniverse" or "ForgottenHall"
            or "PureFiction" or "ApocalypticShadow" or "ChallengePeak");
    }


    [Theory]
    [InlineData("Genshin")]
    [InlineData("GENSHIN")]
    [InlineData("StarRail")]
    [InlineData("ZZZ")]
    public void GameKindIsCaseInsensitive(string gameKind)
    {
        Assert.NotEmpty(ToolboxPageViewModel.GetMenuEntries(gameKind));
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bh3")]
    [InlineData("honkai3")]
    public void UnknownGameHasEmptyMenu(string? gameKind)
    {
        Assert.Empty(ToolboxPageViewModel.GetMenuEntries(gameKind));
    }
}
