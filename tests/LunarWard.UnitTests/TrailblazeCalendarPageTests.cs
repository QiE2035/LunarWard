using LunarWard.App.ViewModels.Starward;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 开拓月历页的展示换算。
/// </summary>
public sealed class TrailblazeCalendarPageTests
{
    [Fact]
    public void Month_item_formats_month_and_hcoin()
    {
        var item = new TrailblazeMonthItem("202610") { Hcoin = 3420 };

        Assert.Equal("2026-10", item.MonthText);
        Assert.Equal("3420", item.HcoinText);
    }


    [Fact]
    public void Month_item_keeps_raw_text_for_unexpected_input()
    {
        Assert.Equal("2026", new TrailblazeMonthItem("2026").MonthText);
    }


    [Fact]
    public void Day_item_exposes_numeric_texts()
    {
        var day = new TrailblazeDayItem { Day = "10-01", Jade = 180, RailsPass = 150, JadeProgress = 1, RailsPassProgress = 1 };

        Assert.Equal("180", day.JadeText);
        Assert.Equal("150", day.RailsPassText);
        Assert.Equal("10-01", day.Day);
    }


    [Fact]
    public void Detail_item_formats_time_and_number()
    {
        var item = new TrailblazeDetailItem(new LunarWard.Core.GameRecord.StarRail.TrailblazeCalendar.TrailblazeCalendarDetailItem
        {
            Type = 1,
            Action = "daily_reward",
            ActionName = "每日实训",
            Time = new DateTime(2026, 10, 7, 4, 30, 0),
            Number = 60,
        });

        Assert.Equal("10-07 04:30", item.TimeText);
        Assert.Equal("每日实训", item.ActionName);
        Assert.Equal("+ 60", item.NumberText);
        Assert.Equal("星琼", item.TypeText);
    }
}
