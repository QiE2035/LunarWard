using LunarWard.ViewModels;
using LunarWard.ViewModels.Starward;
using LunarWard.Games;
using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 游戏选择层「每款游戏一张卡片」的组装（Starward
/// <c>GameSelector.xaml.cs</c> 的 <c>InitializeGameServerArea</c>）。
/// </summary>
public class GameChoiceBuilderTests
{
    private static ProfileItemViewModel Profile(string kind, string edition)
    {
        bool global = edition == "Global";
        return new ProfileItemViewModel(
            $"{kind}-{(global ? "global" : "cn")}",
            new ProfileConfig { Game = kind, Edition = edition, Channel = "Official" },
            kind == GameCatalog.Genshin ? "原神" : "绝区零");
    }


    private static IReadOnlyList<ProfileItemViewModel> AllProfiles()
        => [Profile("genshin", "China"), Profile("genshin", "Global"),
            Profile("zzz", "China"), Profile("zzz", "Global")];


    [Fact]
    public void BuildsOneCardPerGameAndGroupsServersInsideCard()
    {
        var cards = GameChoiceBuilder.Build(AllProfiles(), "zh-cn");

        Assert.Equal(["genshin", "zzz"], cards.Select(x => x.Kind));
        Assert.Equal(["原神", "绝区零"], cards.Select(x => x.Name));

        // 原神：国服官方 + B 站（国服档案支持）+ 国际服官方 → 3 档，全在同一张卡里
        var genshin = cards[0];
        Assert.Equal(3, genshin.Servers.Count);
        Assert.Contains(genshin.Servers, x => x.ProfileId == "genshin-cn" && x.Channel == GameChannel.Official);
        Assert.Contains(genshin.Servers, x => x.ProfileId == "genshin-cn" && x.Channel == GameChannel.Bilibili);
        Assert.Contains(genshin.Servers, x => x.ProfileId == "genshin-global" && x.Channel == GameChannel.Official);
        Assert.False(genshin.IsSingleServer);

        // 绝区零：国服官方 + 国际服官方
        var zzz = cards[1];
        Assert.Equal(2, zzz.Servers.Count);
        Assert.DoesNotContain(zzz.Servers, x => x.Channel == GameChannel.Bilibili);
    }


    [Fact]
    public void ChineseUiUsesChineseGameNamesEvenForGlobalServers()
    {
        var cards = GameChoiceBuilder.Build(AllProfiles(), "zh-cn");
        var globalServer = cards[0].Servers.First(x => x.ProfileId == "genshin-global");

        // 用户反馈的 bug：国际服卡片曾显示 "Genshin"；Starward 显示「原神」。
        Assert.Equal("原神", cards[0].Name);
        Assert.Equal("原神", globalServer.GameName);
        Assert.Equal("国际服", globalServer.Name);
    }


    [Fact]
    public void ServerBadgesDistinguishChinaGlobalAndBilibili()
    {
        var cards = GameChoiceBuilder.Build(AllProfiles(), "zh-cn");
        var servers = cards[0].Servers;

        var china = servers.First(x => x.ProfileId == "genshin-cn" && x.Channel == GameChannel.Official);
        var global = servers.First(x => x.ProfileId == "genshin-global");
        var bilibili = servers.First(x => x.Channel == GameChannel.Bilibili);

        Assert.Equal(("官方国服", "国"), (china.Name, china.BadgeText));
        Assert.Equal(("国际服", "际"), (global.Name, global.BadgeText));
        Assert.Equal(("Bilibili", "B"), (bilibili.Name, bilibili.BadgeText));
        Assert.NotEqual(china.BadgeBrush, global.BadgeBrush);
        Assert.NotEqual(china.BadgeBrush, bilibili.BadgeBrush);
    }


    [Fact]
    public void PinnedServersAreMarkedByProfileAndChannelKey()
    {
        var cards = GameChoiceBuilder.Build(AllProfiles(), "zh-cn",
            pinnedServers: ["genshin-cn:Bilibili"]);

        var pinned = cards[0].Servers.Single(x => x.IsPinned);
        Assert.Equal("genshin-cn", pinned.ProfileId);
        Assert.Equal(GameChannel.Bilibili, pinned.Channel);
        Assert.DoesNotContain(cards[0].Servers, x => x.IsPinned && x != pinned);
    }


    [Fact]
    public void CurrentServerIsMarkedFromProfileAndChannel()
    {
        var cards = GameChoiceBuilder.Build(AllProfiles(), "zh-cn",
            currentProfileId: "genshin-global", currentChannel: GameChannel.Official);

        Assert.True(cards[0].IsCurrent);
        Assert.False(cards[1].IsCurrent);
        Assert.Single(cards[0].Servers, x => x.IsCurrent);
        Assert.Equal("genshin-global", cards[0].Servers.Single(x => x.IsCurrent).ProfileId);
    }


    [Fact]
    public void CardOrderUsesGameKindAndAcceptsLegacyProfileOrGameBizKeys()
    {
        var byKind = GameChoiceBuilder.Build(AllProfiles(), "zh-cn", order: ["zzz", "genshin"]);
        Assert.Equal(["zzz", "genshin"], byKind.Select(x => x.Kind));

        // 旧配置里是档案 id（zzz-global）甚至 gamebiz（hk4e_cn）
        var byLegacyProfile = GameChoiceBuilder.Build(AllProfiles(), "zh-cn", order: ["zzz-global", "genshin-cn"]);
        Assert.Equal(["zzz", "genshin"], byLegacyProfile.Select(x => x.Kind));

        var byLegacyBiz = GameChoiceBuilder.Build(AllProfiles(), "zh-cn", order: ["hk4e_cn"]);
        Assert.Equal(["genshin", "zzz"], byLegacyBiz.Select(x => x.Kind));
    }


    [Theory]
    [InlineData("genshin-cn", "genshin")]
    [InlineData("hk4e_global", "genshin")]
    [InlineData("zzz-global", "zzz")]
    [InlineData("nap_cn", "zzz")]
    [InlineData("genshin", "genshin")]
    [InlineData("unknown", null)]
    [InlineData("", null)]
    public void NormalizeOrderKeyHandlesLegacyKeys(string key, string? expected)
    {
        Assert.Equal(expected, GameChoiceBuilder.NormalizeOrderKey(key));
    }


    [Fact]
    public void SingleServerCardIsFlaggedForDirectSwitch()
    {
        var server = new GameServerItem("崩坏3", "bh3-cn", GameEdition.China, GameChannel.Official,
            "bh3_cn", isInstalled: true, isPinned: false);
        var card = new GameChoiceViewModel("bh3", "崩坏3", [server]);

        Assert.True(card.IsSingleServer);
        Assert.Same(server, card.PrimaryServer);
        Assert.Equal("bh3_cn", card.GameBiz);
    }


    [Fact]
    public void RoundTripKeepsGameIconsPinned()
    {
        string path = Path.Join(Path.GetTempPath(), $"lw-pin-strip-{Guid.NewGuid():N}.json");
        try
        {
            var config = new LauncherConfig();
            config.Launcher.GameIconsPinned = true;
            config.Launcher.GameChoiceOrder.AddRange(["zzz", "genshin"]);
            config.Save(path);

            var loaded = LauncherConfig.Load(path, out string? warning);

            Assert.Null(warning);
            Assert.True(loaded.Launcher.GameIconsPinned);
            Assert.Equal(["zzz", "genshin"], loaded.Launcher.GameChoiceOrder);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
