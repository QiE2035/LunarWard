using LunarWard.Localization;
using LunarWard.Views.Starward;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 「游戏设置 → 游戏包体」的版本文案：标签页标题用游戏版本，副标题用包体清单版本
/// （<c>getGamePackages</c>），两者不能互相覆盖（问题 7：标题曾显示成包体清单的 5.5.0）。
/// </summary>
public class PackageVersionLabelTests
{
    private static AppStrings Strings => AppStrings.Instance;


    [Fact]
    public void ManifestSubtitleCarriesVersionAndSource()
    {
        var text = GameLauncherSettingDialog.FormatManifestSubtitle("5.5.0");

        Assert.Contains("5.5.0", text);
        Assert.Contains("getGamePackages", text);
        // 用的是"游戏包体"，不是标题上的"正式版版本"，避免两个版本号长得一样
        Assert.Contains(Strings["LauncherSetting.GamePackages"], text);
        Assert.DoesNotContain(Strings["LauncherSetting.LatestVersion"], text);
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ManifestSubtitleFallsBackWhenVersionMissing(string? version)
    {
        var text = GameLauncherSettingDialog.FormatManifestSubtitle(version);

        Assert.Contains(Strings["LauncherSetting.GamePackages"], text);
        Assert.Contains(Strings["Common.NoData"], text);
        Assert.DoesNotContain("getGamePackages", text);
    }
}
