using Avalonia.Media;
using LunarWard.ViewModels.Starward;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 模拟宇宙页的换算（罗马数字、祝福底色、记录视图）。
/// </summary>
public sealed class SimulatedUniversePageTests
{
    [Theory]
    [InlineData(1, "Ⅰ")]
    [InlineData(4, "Ⅳ")]
    [InlineData(10, "Ⅹ")]
    [InlineData(11, "")]
    [InlineData(0, "")]
    public void Difficulty_uses_roman_numbers(int difficulty, string expected)
        => Assert.Equal(expected, SimulatedUniversePageViewModel.RomanNumber(difficulty));


    [Fact]
    public void Rank_brush_matches_source_palette()
    {
        Assert.Equal(Color.Parse("#686870"), ((ISolidColorBrush)SimulatedUniversePageViewModel.RankBrush(1)).Color);
        Assert.Equal(Color.Parse("#5379B5"), ((ISolidColorBrush)SimulatedUniversePageViewModel.RankBrush(2)).Color);
        Assert.Equal(Color.Parse("#BC9B6E"), ((ISolidColorBrush)SimulatedUniversePageViewModel.RankBrush(3)).Color);
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)SimulatedUniversePageViewModel.RankBrush(0)).Color);
    }


    [Fact]
    public void Period_item_formats_schedule_begin()
    {
        var item = new SimulatedUniversePeriodItem(
            new LunarWard.Core.GameRecord.StarRail.SimulatedUniverse.SimulatedUniverseRecord
            {
                Basic = new LunarWard.Core.GameRecord.StarRail.SimulatedUniverse.SimulatedUniverseRecordBasic
                {
                    ScheduleId = 1,
                    ScheduleBegin = new DateTime(2026, 10, 1),
                },
            },
            LunarWard.Localization.AppStrings.Instance);

        Assert.Equal("2026-10-01", item.DateText);
    }


    [Fact]
    public void Record_view_formats_score_time_and_children()
    {
        var record = new LunarWard.Core.GameRecord.StarRail.SimulatedUniverse.SimulatedUniverseRecordItem
        {
            Name = "寰宇蝗灾",
            Progress = 3,
            Difficulty = 3,
            Score = 12800,
            FinishTime = new DateTime(2026, 10, 23, 6, 13, 44),
            FinalLineup = [new() { Id = 1, Level = 80, Rarity = 5 }],
            BuffTypeList = [new() { Id = 1, Name = "存护", Cnt = 8 }],
            Miracles = [new() { Id = 1, Name = "奇物 1" }],
        };

        var vm = new SimulatedUniverseItemViewModel(record, LunarWard.Localization.AppStrings.Instance);

        Assert.Equal("Ⅲ", vm.DifficultyText);
        Assert.Equal("12800", vm.ScoreText);
        Assert.Equal("2026-10-23 06:13:44", vm.FinishTimeText);
        Assert.Single(vm.Lineup);
        Assert.Single(vm.BuffTypeList);
        Assert.Equal("8", vm.BuffTypeList[0].CountText);
        Assert.Single(vm.Miracles);
        Assert.False(vm.HasCachedAvatars);
    }
}
