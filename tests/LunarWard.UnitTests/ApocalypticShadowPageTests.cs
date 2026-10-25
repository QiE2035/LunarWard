using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.GameRecord.StarRail.ApocalypticShadow;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 末日幻影页的展示换算。
/// </summary>
public sealed class ApocalypticShadowPageTests
{
    [Fact]
    public void Total_score_sums_three_nodes()
    {
        var floor = new ApocalypticShadowFloorItem(new ApocalypticShadowFloorDetail
        {
            Name = "末日幻影·第 1 关",
            StarNumStr = "3",
            Node1 = new ApocalypticShadowNode { Score = 40000 },
            Node2 = new ApocalypticShadowNode { Score = 32000 },
            Node3 = new ApocalypticShadowNode { Score = 26000 },
        });

        Assert.Equal("98000", floor.TotalScoreText);
        Assert.Equal("40000", floor.Node1ScoreText);
        Assert.Equal("26000", floor.Node3ScoreText);
    }


    [Fact]
    public void Total_score_without_node3()
    {
        var floor = new ApocalypticShadowFloorItem(new ApocalypticShadowFloorDetail
        {
            StarNumStr = "2",
            Node1 = new ApocalypticShadowNode { Score = 30000 },
            Node2 = new ApocalypticShadowNode { Score = 20000 },
        });

        Assert.Equal("50000", floor.TotalScoreText);
        Assert.False(floor.HasNode3);
    }


    [Fact]
    public void Normal_and_extra_stars_split_at_three()
    {
        var floor = new ApocalypticShadowFloorItem(new ApocalypticShadowFloorDetail
        {
            StarNumStr = "4",
            Node1 = new ApocalypticShadowNode { Score = 1 },
            Node2 = new ApocalypticShadowNode { Score = 1 },
        });

        Assert.Equal("★★★", floor.NormalStars);
        Assert.Equal("★", floor.ExtraStars);
        Assert.True(floor.HasExtraStar);
    }


    [Fact]
    public void Fast_clear_hides_teams()
    {
        var floor = new ApocalypticShadowFloorItem(new ApocalypticShadowFloorDetail
        {
            StarNumStr = "3",
            IsFast = true,
            Node1 = new ApocalypticShadowNode { Score = 1 },
            Node2 = new ApocalypticShadowNode { Score = 1 },
        });

        Assert.True(floor.IsFast);
        Assert.False(floor.ShowTeams);
    }


    [Fact]
    public void Boss_defeated_propagates_from_nodes()
    {
        var floor = new ApocalypticShadowFloorItem(new ApocalypticShadowFloorDetail
        {
            StarNumStr = "3",
            Node1 = new ApocalypticShadowNode { BossDefeated = true, Score = 1 },
            Node2 = new ApocalypticShadowNode { BossDefeated = false, Score = 1 },
        });

        Assert.True(floor.Node1BossDefeated);
        Assert.False(floor.Node2BossDefeated);
    }


    [Fact]
    public void Avatar_level_and_rank_are_formatted()
    {
        var avatar = new ApocalypticShadowAvatarItem(new ApocalypticShadowAvatar
        {
            Id = 1,
            Level = 80,
            Rank = 2,
            Rarity = 5,
        });

        Assert.Equal("Lv.80", avatar.LevelText);
        Assert.True(avatar.HasRank);
        Assert.Equal("2", avatar.RankText);
    }
}
