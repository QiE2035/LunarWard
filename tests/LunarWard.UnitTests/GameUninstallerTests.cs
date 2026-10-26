using LunarWard.Services;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 卸载守卫（对应 Starward <c>GameInstaller.UninstallGameAsync</c> 开头的三段判断）。
/// </summary>
public sealed class GameUninstallerTests : IDisposable
{

    private readonly string _root = Path.Join(Path.GetTempPath(), "lw-uninstall-" + Guid.NewGuid().ToString("N"));


    public GameUninstallerTests() => Directory.CreateDirectory(_root);


    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (Exception)
        {
        }
    }


    [Theory]
    [InlineData("", "/data", "/app")]
    [InlineData("/does/not/exist", "/data", "/app")]
    public void Rejects_missing_or_empty_path(string installPath, string dataHome, string app)
    {
        Assert.NotNull(GameUninstaller.Validate(installPath, dataHome, app));
    }


    [Fact]
    public void Rejects_file_system_root()
    {
        Assert.NotNull(GameUninstaller.Validate("/", "/data", "/app"));
    }


    [Fact]
    public void Rejects_when_data_home_is_inside_game()
    {
        var game = Path.Join(_root, "game");
        Directory.CreateDirectory(Path.Join(game, "data"));
        var reason = GameUninstaller.Validate(game, Path.Join(game, "data"), "/app");
        Assert.NotNull(reason);
        Assert.Contains("数据目录", reason);
    }


    [Fact]
    public void Rejects_when_application_is_inside_game()
    {
        var game = Path.Join(_root, "game");
        Directory.CreateDirectory(Path.Join(game, "app"));
        var reason = GameUninstaller.Validate(game, "/data", Path.Join(game, "app"));
        Assert.NotNull(reason);
        Assert.Contains("程序目录", reason);
    }


    [Fact]
    public void Accepts_normal_install_and_deletes_it()
    {
        var game = Path.Join(_root, "game");
        Directory.CreateDirectory(Path.Join(game, "sub"));
        File.WriteAllText(Path.Join(game, "YuanShen.exe"), "x");
        File.WriteAllText(Path.Join(game, "sub", "a.bin"), "y");

        Assert.Null(GameUninstaller.Validate(game, "/data", "/app"));

        var progress = new List<double>();
        GameUninstaller.DeleteDirectory(game, new ProgressReporter(progress));
        Assert.False(Directory.Exists(game));
    }


    [Fact]
    public void Backs_up_screenshots_before_uninstall()
    {
        var game = Path.Join(_root, "game");
        var shots = Path.Join(game, "ScreenShot");
        Directory.CreateDirectory(shots);
        File.WriteAllText(Path.Join(shots, "1.png"), "png");
        var backup = Path.Join(_root, "backup");

        var copied = GameUninstaller.Uninstall(game, "hk4e", backup);

        Assert.Equal(1, copied);
        Assert.True(File.Exists(Path.Join(backup, "1.png")));
        Assert.False(Directory.Exists(game));
    }


    private sealed class ProgressReporter(List<double> sink) : IProgress<double>
    {
        public void Report(double value) => sink.Add(value);
    }

}
