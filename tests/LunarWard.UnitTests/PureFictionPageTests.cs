using LunarWard.ViewModels.Starward;
using LunarWard.Core.GameRecord.StarRail.PureFiction;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 虚构叙事页的纯展示逻辑（Starward <c>PureFictionPage.xaml</c> 每层卡的总分、
/// 节点得分、普通星 / 额外星与快速通关的可见性）。
/// </summary>
public class PureFictionPageTests
{
    private static PureFictionFloorDetail Detail(int starNum, bool fast = false, bool node3 = false)
        => new()
        {
            Name = "虚构叙事 1",
            RoundNum = 4,
            StarNum = starNum,
            IsFast = fast,
            Node1 = new PureFictionNode
            {
                ChallengeTime = new DateTime(2026, 10, 11, 21, 45, 16),
                Score = 40000,
                Avatars = fast ? [] : [new PureFictionAvatar { Id = 1, Level = 80, Rarity = 5 }],
            },
            Node2 = new PureFictionNode
            {
                ChallengeTime = new DateTime(2026, 10, 11, 21, 47, 16),
                Score = 32000,
                Avatars = fast ? [] : [new PureFictionAvatar { Id = 2, Level = 80, Rarity = 4 }],
            },
            Node3 = node3
                ? new PureFictionNode
                {
                    ChallengeTime = new DateTime(2026, 10, 11, 21, 51, 16),
                    Score = 26000,
                    Avatars = [new PureFictionAvatar { Id = 3, Level = 80, Rarity = 5 }],
                }
                : null,
        };


    [Fact]
    public void TotalScore_SumsAllNodes()
    {
        var floor = new PureFictionFloorItem(Detail(3, node3: true));

        Assert.Equal("98000", floor.TotalScoreText);
        Assert.Equal("40000", floor.Node1ScoreText);
        Assert.Equal("32000", floor.Node2ScoreText);
        Assert.Equal("26000", floor.Node3ScoreText);
    }


    [Fact]
    public void TotalScore_IgnoresMissingNode3()
    {
        var floor = new PureFictionFloorItem(Detail(2));

        Assert.Equal("72000", floor.TotalScoreText);
        Assert.False(floor.HasNode3);
        Assert.Equal("", floor.Node3ScoreText);
    }


    [Fact]
    public void Stars_SplitNormalAndExtra()
    {
        var fiveStars = new PureFictionFloorItem(Detail(5));

        Assert.Equal("★★★", fiveStars.NormalStars);
        Assert.Equal("★★", fiveStars.ExtraStars);
        Assert.True(fiveStars.HasExtraStar);

        var twoStars = new PureFictionFloorItem(Detail(2));
        Assert.Equal("★★", twoStars.NormalStars);
        Assert.False(twoStars.HasExtraStar);
    }


    [Fact]
    public void QuickCleared_HidesTeams()
    {
        var floor = new PureFictionFloorItem(Detail(3, fast: true));

        Assert.True(floor.IsFast);
        Assert.False(floor.ShowTeams);
        Assert.Empty(floor.Node1Avatars);
    }


    [Fact]
    public void Teams_ShowAvatarLevels()
    {
        var floor = new PureFictionFloorItem(Detail(3));

        Assert.True(floor.ShowTeams);
        Assert.Equal("Lv.80", floor.Node1Avatars[0].LevelText);
        Assert.False(floor.Node1Avatars[0].HasRank);
        Assert.Equal("2026-10-11 21:45:16", floor.Node1TimeText);
    }


    [Fact]
    public void PeriodItem_FormatsDateAndStars()
    {
        var item = new PureFictionPeriodItem(new PureFictionInfo
        {
            ScheduleId = 200,
            BeginTime = new DateTime(2026, 10, 25, 4, 0, 0),
            EndTime = new DateTime(2026, 11, 8, 4, 0, 0),
            StarNum = 12,
            ExtraStarNum = 3,
        });

        Assert.Equal("2026-10-25", item.DateText);
        Assert.Equal("12", item.StarNumText);
        Assert.Equal("3", item.ExtraStarNumText);
        Assert.True(item.HasExtraStar);
    }
}
