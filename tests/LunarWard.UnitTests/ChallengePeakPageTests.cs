using LunarWard.App.ViewModels.Starward;
using LunarWard.Core.GameRecord.StarRail.ChallengePeak;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 异象仲裁页的展示换算。
/// </summary>
public sealed class ChallengePeakPageTests
{
    [Fact]
    public void Period_item_prefers_game_version()
    {
        var item = new ChallengePeakPeriodItem(
            new ChallengePeakRecord
            {
                Group = new ChallengePeakGroup { GroupId = 1, GameVersion = "4.6", NameMi18n = "异象仲裁·静止的审判" },
                BossStars = 3,
                MobStars = 8,
            },
            null);

        Assert.Equal("4.6", item.GameVersion);
        Assert.Equal("3", item.BossStars);
        Assert.Equal("8", item.MobStars);
    }


    [Fact]
    public void Period_item_falls_back_to_group_name()
    {
        var item = new ChallengePeakPeriodItem(
            new ChallengePeakRecord
            {
                Group = new ChallengePeakGroup { GroupId = 1, GameVersion = "", NameMi18n = "异象仲裁" },
            },
            null);

        Assert.Equal("异象仲裁", item.GameVersion);
    }


    [Fact]
    public void Mob_item_formats_name_and_stars()
    {
        var item = new ChallengePeakMobItem(new ChallengePeakRecordMob(
            new ChallengePeakMobInfo { MazeId = 1, Name = "第一位骑士", MonsterName = "· 灰熊" },
            new ChallengePeakMobRecord { MazeId = 1, HasChallengeRecord = true, RoundNum = 2, StarNum = 3 }));

        Assert.Equal("第一位骑士· 灰熊", item.NameText);
        Assert.Equal("2", item.RoundNumText);
        Assert.Equal("★★★", item.StarGlyph);
        Assert.True(item.ShowTeam);
    }


    [Fact]
    public void Fast_clear_hides_team()
    {
        var item = new ChallengePeakMobItem(new ChallengePeakRecordMob(
            new ChallengePeakMobInfo { MazeId = 1, Name = "第二位骑士", MonsterName = "· 迷失者" },
            new ChallengePeakMobRecord { MazeId = 1, HasChallengeRecord = true, IsFast = true, StarNum = 2 }));

        Assert.True(item.IsFast);
        Assert.False(item.ShowTeam);
    }


    [Fact]
    public void Mob_without_record_shows_no_clearance()
    {
        var item = new ChallengePeakMobItem(new ChallengePeakRecordMob(
            new ChallengePeakMobInfo { MazeId = 2, Name = "第三位骑士", MonsterName = "" },
            null));

        Assert.False(item.HasChallengeRecord);
        Assert.Equal("", item.StarGlyph);
        Assert.Equal("", item.RoundNumText);
    }


    [Fact]
    public void Avatar_level_and_rank_are_formatted()
    {
        var avatar = new ChallengePeakAvatarItem(new ChallengePeakAvatar { Id = 1, Level = 80, Rank = 1, Rarity = 4 });

        Assert.Equal("Lv.80", avatar.LevelText);
        Assert.True(avatar.HasRank);
        Assert.Equal("1", avatar.RankText);
    }
}
