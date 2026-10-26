using LunarWard.Core;
using LunarWard.Core.HoYoPlay;
using LunarWard.Features.GameLauncher;
using Xunit;

namespace LunarWard.UnitTests.Features.GameLauncher;

/// <summary>
/// 启动链路里与 Windows 无关的纯逻辑：可执行文件名映射、wine 启动信息（工作目录、命令行、
/// 基线环境注入）、HDR 之外的参数顺序。断言直接对着上游 <c>GameLauncherService</c> 的语义写，
/// 不启动真实进程。
/// </summary>
public sealed class GameLauncherServiceTests
{
    private static GameId Id(string gameBiz)
    {
        Assert.True(GameBiz.TryParse(gameBiz, out GameBiz biz));
        GameId? id = GameId.FromGameBiz(biz);
        Assert.NotNull(id);
        return id!;
    }


    [Theory]
    [InlineData("hk4e_cn", "YuanShen.exe")]
    [InlineData("hk4e_bilibili", "YuanShen.exe")]
    [InlineData("hk4e_global", "GenshinImpact.exe")]
    [InlineData("hkrpg_cn", "StarRail.exe")]
    [InlineData("hkrpg_global", "StarRail.exe")]
    [InlineData("bh3_cn", "BH3.exe")]
    [InlineData("nap_cn", "ZenlessZoneZero.exe")]
    public void GetGameExeNameMatchesUpstream(string biz, string expected)
    {
        Assert.Equal(expected, GameLauncherService.GetGameExeName(new GameBiz(biz)));
    }


    [Fact]
    public void BuildStartInfoRunsExeThroughWine()
    {
        GameId id = Id("hk4e_cn");
        string? oldPrefix = GamePrefixContext.CurrentPrefix;
        string? oldWine = GamePrefixContext.CurrentWineBinary;
        GamePrefixContext.CurrentPrefix = "/tmp/prefix";
        GamePrefixContext.CurrentWineBinary = "/usr/bin/wine";

        try
        {
            var info = GameLauncherService.BuildStartInfo(id, "/games/Genshin/YuanShen.exe", "-popupwindow -use-d3d12");

            Assert.Equal("/usr/bin/wine", info.FileName);
            // 上游把整条命令行交给 CreateProcess；这里必须保持单字符串语义（引号包住 exe）。
            Assert.Equal("\"/games/Genshin/YuanShen.exe\" -popupwindow -use-d3d12", info.Arguments);
            Assert.False(info.UseShellExecute);
            Assert.Equal("/games/Genshin", info.WorkingDirectory);
            Assert.Equal("/tmp/prefix", info.Environment["WINEPREFIX"]);
            Assert.Equal("win64", info.Environment["WINEARCH"]);
        }
        finally
        {
            GamePrefixContext.CurrentPrefix = oldPrefix;
            GamePrefixContext.CurrentWineBinary = oldWine;
        }
    }


    [Fact]
    public void BuildStartInfoUsesWineWhenNoBinaryConfigured()
    {
        GameId id = Id("hkrpg_cn");
        string? oldWine = GamePrefixContext.CurrentWineBinary;
        GamePrefixContext.CurrentWineBinary = null;

        try
        {
            var info = GameLauncherService.BuildStartInfo(id, "/games/SR/StarRail.exe", null);

            Assert.Equal("wine", info.FileName);
            Assert.Equal("\"/games/SR/StarRail.exe\"", info.Arguments);
        }
        finally
        {
            GamePrefixContext.CurrentWineBinary = oldWine;
        }
    }


    [Fact]
    public void BuildStartInfoRunsNativeExecutableDirectly()
    {
        GameId id = Id("hk4e_cn");

        var info = GameLauncherService.BuildStartInfo(id, "/games/Genshin/launch.sh", "--foo");

        Assert.Equal("/games/Genshin/launch.sh", info.FileName);
        Assert.Equal("--foo", info.Arguments);
        Assert.False(info.Environment.ContainsKey("WINEPREFIX"));
    }


    [Fact]
    public void BuildStartInfoInjectsDx12EnvironmentOnlyForZzz()
    {
        GameId zzz = Id("nap_cn");
        GameId genshin = Id("hk4e_cn");

        var zzzInfo = GameLauncherService.BuildStartInfo(zzz, "/games/ZZZ/ZenlessZoneZero.exe", null);
        var genshinInfo = GameLauncherService.BuildStartInfo(genshin, "/games/Genshin/YuanShen.exe", null);

        // 上游在 Windows 上走原生 DX12；Linux 经 DXVK/VKD3D 才需要这两个变量（GameAdapter 口径）。
        Assert.Equal("1", zzzInfo.Environment["DXVK_ENABLE_NVAPI"]);
        Assert.Equal("dxr", zzzInfo.Environment["VKD3D_CONFIG"]);
        Assert.False(genshinInfo.Environment.ContainsKey("DXVK_ENABLE_NVAPI"));
    }
}
