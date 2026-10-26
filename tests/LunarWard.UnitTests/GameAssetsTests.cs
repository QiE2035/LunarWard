using LunarWard.Services;
using LunarWard.Features.GameLauncher;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 品牌素材路径映射（上游 <c>GameBizIcon.GameBizToServerIcon</c> / <c>GameBizToIcon</c>）。
/// 素材本身来自 <c>Starward.Assets</c> 内容包，这里只校验落盘位置与文件名约定。
/// </summary>
public class GameAssetsTests
{
    [Theory]
    [InlineData(GameEdition.China, GameChannel.Official, "gameicon_hyperion.png")]
    [InlineData(GameEdition.Global, GameChannel.Official, "gameicon_hoyolab.png")]
    [InlineData(GameEdition.China, GameChannel.Bilibili, "gameicon_bilibili.png")]
    [InlineData(GameEdition.Global, GameChannel.Bilibili, "gameicon_bilibili.png")]
    public void ServerIconFileNameMatchesUpstreamServerBranch(GameEdition edition, GameChannel channel, string expected)
    {
        Assert.Equal(expected, GameAssets.ServerIconFileName(edition, channel));
    }


    [Fact]
    public void ServerIconPathLivesUnderOutputAssetsImage()
    {
        string path = GameAssets.ServerIconPath(GameEdition.China, GameChannel.Official);

        Assert.EndsWith(Path.Join("Assets", "Image", "gameicon_hyperion.png"), path, StringComparison.Ordinal);
        Assert.StartsWith(Path.GetFullPath(AppContext.BaseDirectory), Path.GetFullPath(path), StringComparison.Ordinal);
    }


    [Theory]
    [InlineData(GameCatalog.Genshin, "icon_ys.jpg")]
    [InlineData(GameCatalog.Zzz, "icon_zzz.jpg")]
    [InlineData("unknown-game", "Transparent.png")]
    public void GameIconFileNameMatchesUpstreamGameBranch(string kind, string expected)
    {
        Assert.Equal(expected, GameAssets.GameIconFileName(kind));
    }
}
