using LunarWard.Launch;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 游戏选择器「区服固定」（Starward <c>GameBizIcon.IsPinned</c> → <c>AppConfig</c>）的配置往返。
/// </summary>
public class GameServerPinTests
{

    [Fact]
    public void DefaultsToNoPinnedServers()
    {
        var settings = new LauncherSettings();

        Assert.Empty(settings.PinnedGameServers);
    }


    [Fact]
    public void RoundTripKeepsCardOrder()
    {
        string path = Path.Join(Path.GetTempPath(), $"lw-order-{Guid.NewGuid():N}.json");
        try
        {
            var config = new LauncherConfig();
            config.Launcher.GameChoiceOrder.AddRange(["zzz-global", "hk4e_cn"]);
            config.Save(path);

            var loaded = LauncherConfig.Load(path, out string? warning);

            Assert.Null(warning);
            Assert.Equal(["zzz-global", "hk4e_cn"], loaded.Launcher.GameChoiceOrder);
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public void RoundTripKeepsPinnedServers()
    {
        string path = Path.Join(Path.GetTempPath(), $"lw-servers-{Guid.NewGuid():N}.json");
        try
        {
            var config = new LauncherConfig();
            config.Launcher.PinnedGameServers.Add("hk4e_cn:Bilibili");
            config.Save(path);

            var loaded = LauncherConfig.Load(path, out string? warning);

            Assert.Null(warning);
            Assert.Contains("hk4e_cn:Bilibili", loaded.Launcher.PinnedGameServers);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
