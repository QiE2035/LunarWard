using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>截图目录发现（文件层面，不做图像解码）。</summary>
public class ScreenshotTests
{
    private static string MakeScreenshot(TempDirectory temp, string gameDir, string kind, string name, DateTime time, int size = 16)
    {
        var dir = ScreenshotService.GetDirectory(gameDir, kind);
        Directory.CreateDirectory(dir);
        var path = Path.Join(dir, name);
        File.WriteAllBytes(path, new byte[size]);
        File.SetLastWriteTimeUtc(path, time.ToUniversalTime());
        return path;
    }


    [Fact]
    public void GenshinAndZzz_UseScreenShotDirectory()
    {
        Assert.Equal("ScreenShot", ScreenshotService.RelativeDirectory("genshin"));
        Assert.Equal("ScreenShot", ScreenshotService.RelativeDirectory("zzz"));
        Assert.Equal(Path.Join("StarRail_Data", "ScreenShots"), ScreenshotService.RelativeDirectory("hkrpg"));
    }


    [Fact]
    public void List_ReturnsNewestFirst()
    {
        using var temp = new TempDirectory();
        var game = temp.Combine("game");
        MakeScreenshot(temp, game, "genshin", "old.png", DateTime.Now.AddDays(-2));
        MakeScreenshot(temp, game, "genshin", "new.png", DateTime.Now);

        var list = new ScreenshotService().List(game, "genshin");
        Assert.Equal(2, list.Count);
        Assert.Equal("new.png", list[0].FileName);
    }


    [Fact]
    public void List_FiltersNonImages()
    {
        using var temp = new TempDirectory();
        var game = temp.Combine("game");
        var dir = ScreenshotService.GetDirectory(game, "genshin");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Join(dir, "a.txt"), "x");
        File.WriteAllText(Path.Join(dir, "b.png"), "x");
        File.WriteAllText(Path.Join(dir, "c.JPG"), "x");

        var list = new ScreenshotService().List(game, "genshin");
        Assert.Equal(2, list.Count);
        Assert.Contains(list, x => x.FileName == "c.JPG"); // 扩展名匹配大小写不敏感
    }


    [Fact]
    public void List_MissingDirectory_IsEmptyNotThrow()
    {
        using var temp = new TempDirectory();
        Assert.Empty(new ScreenshotService().List(temp.Combine("nope"), "genshin"));
    }


    [Fact]
    public void List_HonoursMax()
    {
        using var temp = new TempDirectory();
        var game = temp.Combine("game");
        for (var i = 0; i < 10; i++)
        {
            MakeScreenshot(temp, game, "genshin", $"s{i}.png", DateTime.Now.AddMinutes(-i));
        }

        Assert.Equal(3, new ScreenshotService().List(game, "genshin", max: 3).Count);
    }


    [Theory]
    [InlineData("100000000_2026-10-23_12-00-00.png", "100000000")]
    [InlineData("123456789.png", "123456789")]
    [InlineData("screenshot.png", null)]
    [InlineData("12345.png", null)]  // 不足 6 位
    public void ExtractUid_ReadsLeadingDigits(string fileName, string? expected)
    {
        Assert.Equal(expected, ScreenshotService.ExtractUid(fileName));
    }


    [Fact]
    public void CopyTo_AvoidsOverwrite()
    {
        using var temp = new TempDirectory();
        var game = temp.Combine("game");
        var path = MakeScreenshot(temp, game, "genshin", "a.png", DateTime.Now);
        var service = new ScreenshotService();
        var screenshot = service.List(game, "genshin")[0];

        var target = temp.Combine("export");
        var first = service.CopyTo(screenshot, target);
        var second = service.CopyTo(screenshot, target);

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(path)); // 原文件保留
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.Contains("(1)", second);
    }


    [Fact]
    public void Summarize_CountsAndSums()
    {
        using var temp = new TempDirectory();
        var game = temp.Combine("game");
        MakeScreenshot(temp, game, "genshin", "a.png", DateTime.Now, size: 100);
        MakeScreenshot(temp, game, "genshin", "b.png", DateTime.Now, size: 250);

        var (count, bytes) = new ScreenshotService().Summarize(game, "genshin");
        Assert.Equal(2, count);
        Assert.Equal(350, bytes);
    }
}
