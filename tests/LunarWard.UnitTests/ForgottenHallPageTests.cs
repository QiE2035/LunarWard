using LunarWard.ViewModels.Starward;
using LunarWard.Core.GameRecord.StarRail.ForgottenHall;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 忘却之庭页的纯展示逻辑（Starward <c>ForgottenHallPage.xaml</c> 每层卡的
/// 普通星 / 额外星 / 使用轮次，以及左侧列表的日期与星数行）。
/// </summary>
public class ForgottenHallPageTests
{
    private static ForgottenHallFloorDetail Detail(int starNum, int roundNum = 5, bool withAvatars = true)
        => new()
        {
            Name = "回忆其十二",
            RoundNum = roundNum,
            StarNum = starNum,
            Node1 = new ForgottenHallNode
            {
                ChallengeTime = new DateTime(2026, 10, 11, 21, 34, 52),
                Avatars = withAvatars ? [new ForgottenHallAvatar { Id = 1, Level = 80, Rarity = 5 }] : [],
            },
            Node2 = new ForgottenHallNode
            {
                ChallengeTime = new DateTime(2026, 10, 11, 21, 36, 52),
                Avatars = withAvatars ? [new ForgottenHallAvatar { Id = 2, Level = 80, Rarity = 4 }] : [],
            },
        };


    [Fact]
    public void FloorStars_SplitIntoSixMaxNormalAndExtra()
    {
        var floor = new ForgottenHallFloorItem(Detail(5));

        // Starward：NormalStarNum = Min(StarNum, 3)，多出来的算额外星。
        Assert.Equal("★★★", floor.NormalStars);
        Assert.Equal("★★", floor.ExtraStars);
        Assert.True(floor.HasExtraStar);
    }


    [Fact]
    public void FloorStars_ThreeOrLessHaveNoExtraStar()
    {
        var floor = new ForgottenHallFloorItem(Detail(2));

        Assert.Equal("★★", floor.NormalStars);
        Assert.Equal("", floor.ExtraStars);
        Assert.False(floor.HasExtraStar);
    }


    [Fact]
    public void FloorQuickCleared_WhenNodesHaveNoAvatars()
    {
        var floor = new ForgottenHallFloorItem(Detail(3, withAvatars: false));

        Assert.True(floor.IsFast);
        Assert.False(floor.ShowTeams);
    }


    [Fact]
    public void FloorTeams_ShownWhenAvatarsExist()
    {
        var floor = new ForgottenHallFloorItem(Detail(3));

        Assert.False(floor.IsFast);
        Assert.True(floor.ShowTeams);
        Assert.Single(floor.Node1Avatars);
        Assert.Equal("Lv.80", floor.Node1Avatars[0].LevelText);
        Assert.False(floor.Node1Avatars[0].HasRank);
        Assert.Equal("2026-10-11 21:34:52", floor.Node1TimeText);
    }


    [Fact]
    public void PeriodItem_FormatsDateAndStars()
    {
        var item = new ForgottenHallPeriodItem(new ForgottenHallInfo
        {
            ScheduleId = 100,
            BeginTime = new DateTime(2026, 10, 25, 4, 0, 0),
            EndTime = new DateTime(2026, 11, 8, 4, 0, 0),
            StarNum = 30,
            ExtraStarNum = 6,
        });

        Assert.Equal("2026-10-25", item.DateText);
        Assert.Equal("30", item.StarNumText);
        Assert.Equal("6", item.ExtraStarNumText);
        Assert.True(item.HasExtraStar);
    }
}
