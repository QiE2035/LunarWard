using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.GameLauncher;
using LunarWard.Features.GameSelector;
using Xunit;

namespace LunarWard.UnitTests.Features.GameLauncher;

/// <summary>
/// 启动器簇里的纯展示模型：图标（<c>GameBizIcon</c> 默认值、尺寸文案、选中遮罩）与公告分组。
/// 上游同名文件逐行移植，这里锁住格式化口径（<c>{TotalSize/GB:F2}GB</c>、<c>0 → null</c>）。
/// </summary>
public sealed class GameSelectorModelTests
{
    [Fact]
    public void GameBizIconDefaultsMatchUpstream()
    {
        var icon = new GameBizIcon(new GameBiz("hk4e_cn"));

        Assert.Equal(1.0, icon.MaskOpacity);
        Assert.Equal(0, icon.TotalSize);
        Assert.Null(icon.TotalSizeText);
        Assert.False(icon.IsPinned);
        Assert.False(icon.IsSelected);
        Assert.Contains("icon_", icon.GameIcon);
        Assert.Contains("gameicon_hyperion", icon.ServerIcon);
    }


    [Theory]
    [InlineData(1L << 30, "1.00GB")]
    [InlineData(3L << 29, "1.50GB")]
    [InlineData(5L << 30, "5.00GB")]
    public void TotalSizeTextUsesBinaryGigabytes(long bytes, string expected)
    {
        var icon = new GameBizIcon(new GameBiz("hk4e_cn")) { TotalSize = bytes };

        Assert.Equal(expected, icon.TotalSizeText);
    }


    [Fact]
    public void SelectingIconTogglesMaskOpacity()
    {
        var icon = new GameBizIcon(new GameBiz("hkrpg_global"));

        icon.IsSelected = true;
        Assert.Equal(0, icon.MaskOpacity);

        icon.IsSelected = false;
        Assert.Equal(1, icon.MaskOpacity);
    }


    [Fact]
    public void SuggestionUidHandlingUsesLong()
    {
        // Uid 在 GameAccount 里是 long（上游同），GameAccountService.GetSuggestionUids 也用 long。
        var account = new LunarWard.Features.GameAccount.GameAccount
        {
            SHA256 = "AB",
            GameBiz = new GameBiz("hk4e_cn"),
            Uid = 4_000_000_000,
            Name = "tester",
            Value = [1, 2, 3],
        };

        Assert.Equal(4_000_000_000, account.Uid);
        Assert.True(account.IsSaved == false);
    }


    [Fact]
    public void FromGameContentGroupsPostsByType()
    {
        var content = new GameContent
        {
            Posts =
            [
                new GamePost { Type = GamePostType.POST_TYPE_ANNOUNCE, Title = "a1" },
                new GamePost { Type = GamePostType.POST_TYPE_ACTIVITY, Title = "b1" },
                new GamePost { Type = GamePostType.POST_TYPE_ANNOUNCE, Title = "a2" },
            ],
        };

        var groups = GamePostGroup.FromGameContent(content);

        Assert.Equal(2, groups.Count);
        // 上游 GroupBy(...).OrderBy(x => x.Key)：Activity 在 Announce 之前。
        Assert.Equal(GamePostType.POST_TYPE_ACTIVITY, groups[0].List[0].Type);
        Assert.Equal(2, groups[1].List.Count);
    }
}
