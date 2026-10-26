using LunarWard.App.Services;
using LunarWard.Core.HoYoPlay;
using Xunit;

namespace LunarWard.UnitTests;

public class BackgroundCatalogTests
{
    private static GameImage Image(string url) => new() { Url = url };

    private static GameBackground Background(string image, string? video = null, string? theme = null, string type = "BACKGROUND_TYPE_UNSPECIFIED")
        => new()
        {
            Id = "test",
            Background = Image(image),
            Video = video is null ? null! : Image(video),
            Theme = theme is null ? null! : Image(theme),
            Type = type,
        };


    [Fact]
    public void FromApi_CarriesThemePosterOnImageAndVideoEntries()
    {
        var info = new GameBackgroundInfo
        {
            Backgrounds =
            [
                Background("https://x/bg.png", theme: "https://x/theme.png"),
                Background("https://x/poster.png", "https://x/v.webm", "https://x/theme2.png",
                    GameBackground.BACKGROUND_TYPE_VIDEO),
            ],
        };

        var entries = BackgroundCatalog.FromApi(info);

        Assert.Equal(2, entries.Count);
        Assert.Equal("https://x/theme.png", entries[0].ThemeSource);
        Assert.False(entries[0].IsVideo);
        Assert.Equal("https://x/theme2.png", entries[1].ThemeSource);
        Assert.True(entries[1].IsVideo);
    }


    [Fact]
    public void FromApi_MissingThemeStaysNull()
    {
        var entries = BackgroundCatalog.FromApi(new GameBackgroundInfo
        {
            Backgrounds = [Background("https://x/bg.png")],
        });

        Assert.Single(entries);
        Assert.Null(entries[0].ThemeSource);
    }


    [Fact]
    public void Merge_KeepsOfficialThemeAndLeavesPosterAndCustomWithout()
    {
        var official = new[]
        {
            new BackgroundEntry(BackgroundKind.Official, "https://x/bg.png", null, "官方背景", "https://x/theme.png"),
        };
        var custom = BackgroundCatalog.FromFiles(["/tmp/a.png"]);

        var merged = BackgroundCatalog.Merge(official, "https://x/poster.png", custom);

        Assert.Equal(3, merged.Count);
        Assert.Equal("https://x/theme.png", merged[0].ThemeSource);
        Assert.Null(merged[1].ThemeSource);
        Assert.All(merged.Skip(2), x => Assert.Null(x.ThemeSource));
    }
}
