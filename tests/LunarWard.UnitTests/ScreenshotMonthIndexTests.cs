using LunarWard.ViewModels.Starward;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>截图月份胶囊的纯计算（对应上游 AnnotatedScrollBar 的分组标注）。</summary>
public sealed class ScreenshotMonthIndexTests
{
    private static readonly (double Y, string Month)[] Map =
    [
        (0, "2026-06"),
        (480, "2026-07"),
        (960, "2026-07"),
        (1440, "2026-08"),
    ];

    [Theory]
    [InlineData(0, "2026-06")]
    [InlineData(120, "2026-06")]
    [InlineData(479, "2026-06")]
    [InlineData(480, "2026-07")]
    [InlineData(1200, "2026-07")]
    [InlineData(1440, "2026-08")]
    [InlineData(99999, "2026-08")]
    public void Locate_ReturnsMonthOfLastEntryAtOrAboveOffset(double offset, string expected)
    {
        Assert.Equal(expected, ScreenshotMonthIndex.Locate(Map, offset));
    }

    [Fact]
    public void Locate_EmptyMap_ReturnsNull()
    {
        Assert.Null(ScreenshotMonthIndex.Locate([], 100));
    }

    [Fact]
    public void Locate_NegativeOffset_ReturnsFirstEntry()
    {
        Assert.Equal("2026-06", ScreenshotMonthIndex.Locate(Map, -50));
    }

    [Fact]
    public void Label_FormatsAsYearDashTwoDigitMonth()
    {
        Assert.Equal("2026-06", ScreenshotMonthIndex.Label(new DateTimeOffset(2026, 6, 18, 10, 0, 0, TimeSpan.FromHours(8))));
        Assert.Equal("2025-12", ScreenshotMonthIndex.Label(new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero)));
    }
}
