using LunarWard.App;
using LunarWard.App.ViewModels.Starward;
using LunarWard.Launch;
using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 左上角「固定」开关的持久化：启动时从 <see cref="LauncherSettings.GameIconsPinned"/> 恢复，
/// 用户切换时写回配置，重建图标条（<see cref="ShellViewModel.ReloadProfiles"/>）不重置状态。
/// </summary>
/// <remarks>
/// 构造 <see cref="ShellViewModel"/> 会发后不管地访问网络（素材/更新检查），但同步路径只需要
/// 一份「已有默认运行时」的配置来跳过 <c>RuntimeScanner</c> 扫描，避免测试写盘、拖慢。
/// </remarks>
[Collection(GlobalStaticsCollection.Name)]
public sealed class ShellViewModelPinnedIconTests : IDisposable
{
    [Fact]
    public void RestoresPinnedFlagFromConfigWithoutRewritingIt()
    {
        using var temp = new TempDirectory("lunarward-pinned-");
        var paths = CreatePaths(temp);
        var config = new LauncherConfig();
        config.Launcher.GameIconsPinned = true;
        SeedDefaultRuntime(config);
        config.Save(paths.ConfigFile);
        var before = File.ReadAllText(paths.ConfigFile);

        var services = new AppServices(paths);
        using var shell = new ShellViewModel(services, services.Log);

        Assert.True(shell.IsGameIconsPinned);

        // 恢复只写字段/属性，不应顺手再写一次盘。
        Assert.Equal(before, File.ReadAllText(paths.ConfigFile));
    }


    [Fact]
    public void TogglingPinnedFlagPersistsToConfig()
    {
        using var temp = new TempDirectory("lunarward-pinned-");
        var paths = CreatePaths(temp);
        var config = new LauncherConfig();
        config.Launcher.GameIconsPinned = false;
        SeedDefaultRuntime(config);
        config.Save(paths.ConfigFile);

        var services = new AppServices(paths);
        using var shell = new ShellViewModel(services, services.Log);
        Assert.False(shell.IsGameIconsPinned);

        shell.IsGameIconsPinned = true;

        Assert.True(shell.IsGameIconsVisible);
        Assert.True(LauncherConfig.Load(paths.ConfigFile, out _).Launcher.GameIconsPinned);
    }


    [Fact]
    public void ReloadingProfilesKeepsPinnedState()
    {
        using var temp = new TempDirectory("lunarward-pinned-");
        var paths = CreatePaths(temp);
        var config = new LauncherConfig();
        config.Launcher.GameIconsPinned = true;
        SeedDefaultRuntime(config);
        config.Save(paths.ConfigFile);

        var services = new AppServices(paths);
        using var shell = new ShellViewModel(services, services.Log);

        // 切游戏/换区服会重建图标条；「固定」是用户偏好，不该被重置。
        shell.ReloadProfiles();

        Assert.True(shell.IsGameIconsPinned);
        Assert.True(shell.IsGameIconsVisible);
        Assert.True(LauncherConfig.Load(paths.ConfigFile, out _).Launcher.GameIconsPinned);
    }


    private static XdgPaths CreatePaths(TempDirectory temp)
        => new(temp.Combine("config"), temp.Combine("data"), temp.Combine("cache"));


    /// <summary>预置一个默认运行时，让 <c>EnsureDefaultRuntime</c> 直接返回（不扫描、不写盘）。</summary>
    private static void SeedDefaultRuntime(LauncherConfig config)
    {
        config.Runtimes["test"] = new RuntimeConfigDto
        {
            Runner = new RunnerDto { Kind = "Custom", Path = "/nonexistent/lunarward-test-wine" },
        };
        config.Launcher.DefaultRuntime = "test";
    }

    /// <summary>
    /// <see cref="AppServices"/> 会把安装路径提供者塞进 <c>GachaLogClient</c> 的静态属性，
    /// 这里在用例结束后还原，避免污染同一集合里的其它用例。
    /// </summary>
    public void Dispose() => LunarWard.Core.Gacha.GachaLogClient.InstallPathProvider = null;
}
