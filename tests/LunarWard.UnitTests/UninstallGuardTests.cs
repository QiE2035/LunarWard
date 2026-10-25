using LunarWard.App.Services;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 卸载守卫（<see cref="GameUninstaller.Validate"/>）：卸载游戏前把「会误删用户数据」的路径挡下来。
/// </summary>
public class UninstallGuardTests
{
    [Fact]
    public void Empty_path_is_rejected()
    {
        Assert.NotNull(GameUninstaller.Validate("", "/tmp/data", "/tmp/app"));
        Assert.NotNull(GameUninstaller.Validate(null, "/tmp/data", "/tmp/app"));
    }


    [Fact]
    public void Missing_directory_is_rejected()
    {
        string missing = Path.Join(Path.GetTempPath(), "lw-missing-" + Guid.NewGuid().ToString("N"));
        Assert.NotNull(GameUninstaller.Validate(missing, "/tmp/data", "/tmp/app"));
    }


    [Fact]
    public void Data_home_inside_game_directory_is_rejected()
    {
        string game = Directory.CreateTempSubdirectory("lw-game-").FullName;
        try
        {
            string dataHome = Path.Join(game, "data");
            Assert.NotNull(GameUninstaller.Validate(game, dataHome, "/tmp/app"));
        }
        finally
        {
            Directory.Delete(game, true);
        }
    }


    [Fact]
    public void Ordinary_game_directory_is_allowed()
    {
        string game = Directory.CreateTempSubdirectory("lw-game-").FullName;
        try
        {
            Assert.Null(GameUninstaller.Validate(game, "/tmp/lw-home-not-inside", "/tmp/lw-app-not-inside"));
        }
        finally
        {
            Directory.Delete(game, true);
        }
    }
}
