using System.Text;
using LunarWard.Launch;
using LunarWard.Platform;
using LunarWard.Wine;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>DXVK 安装与状态检测（读 <c>user.reg</c> 文本，不依赖 wine 进程）。</summary>
public class DxvkTests
{
    private static string CreateDxvkTree(TempDirectory temp)
    {
        var dir = temp.Combine("dxvk");
        Directory.CreateDirectory(Path.Join(dir, "x64"));
        Directory.CreateDirectory(Path.Join(dir, "x32"));
        foreach (var dll in new[] { "dxgi", "d3d9", "d3d10core", "d3d11" })
        {
            File.WriteAllText(Path.Join(dir, "x64", dll + ".dll"), "x64-" + dll);
            File.WriteAllText(Path.Join(dir, "x32", dll + ".dll"), "x32-" + dll);
        }

        return dir;
    }


    [Fact]
    public async Task Install_WritesDllsBacksUpAndSetsRegistry()
    {
        using var temp = new TempDirectory();
        var prefix = temp.CreatePrefix();
        var system32 = Path.Join(prefix, "drive_c", "windows", "system32");
        Directory.CreateDirectory(system32);

        // 预置 wine 自带的 dxgi.dll → 应被备份
        File.WriteAllText(Path.Join(system32, "dxgi.dll"), "wine-original");

        var dxvk = CreateDxvkTree(temp);
        var manager = new DxvkManager();

        Assert.False(manager.IsInstalled(prefix));
        await manager.InstallAsync(prefix, dxvk);

        Assert.True(manager.IsInstalled(prefix));
        Assert.Equal("x64-dxgi", File.ReadAllText(Path.Join(system32, "dxgi.dll")));
        Assert.Equal("wine-original", File.ReadAllText(Path.Join(system32, "dxgi.dll.old")));

        var userReg = File.ReadAllText(Path.Join(prefix, "user.reg"));
        Assert.Contains("DllOverrides", userReg);
        foreach (var dll in manager.DllNames)
        {
            Assert.Contains($"\"{dll}\"=\"native,builtin\"", userReg);
        }
    }


    [Fact]
    public async Task Install_IsIdempotent()
    {
        using var temp = new TempDirectory();
        var prefix = temp.CreatePrefix();
        var dxvk = CreateDxvkTree(temp);
        var manager = new DxvkManager();

        await manager.InstallAsync(prefix, dxvk);
        var first = File.ReadAllText(Path.Join(prefix, "user.reg"));

        await manager.InstallAsync(prefix, dxvk);
        var second = File.ReadAllText(Path.Join(prefix, "user.reg"));

        Assert.Equal(first, second);
        // 不能出现重复的键
        Assert.Equal(1, CountOccurrences(second, "\"dxgi\""));
    }


    [Fact]
    public async Task Uninstall_RestoresBackupAndClearsOverrides()
    {
        using var temp = new TempDirectory();
        var prefix = temp.CreatePrefix();
        var system32 = Path.Join(prefix, "drive_c", "windows", "system32");
        Directory.CreateDirectory(system32);
        File.WriteAllText(Path.Join(system32, "dxgi.dll"), "wine-original");

        var dxvk = CreateDxvkTree(temp);
        var manager = new DxvkManager();
        await manager.InstallAsync(prefix, dxvk);
        await manager.UninstallAsync(prefix);

        Assert.Equal("wine-original", File.ReadAllText(Path.Join(system32, "dxgi.dll")));
        Assert.False(manager.IsInstalled(prefix));
    }


    [Fact]
    public async Task Install_RejectsInvalidPrefix()
    {
        using var temp = new TempDirectory();
        var dxvk = CreateDxvkTree(temp);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new DxvkManager().InstallAsync(temp.Combine("nope"), dxvk));
    }


    [Fact]
    public async Task Install_RejectsMissingDxvkDirectory()
    {
        using var temp = new TempDirectory();
        var prefix = temp.CreatePrefix();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => new DxvkManager().InstallAsync(prefix, temp.Combine("missing")));
    }


    [Fact]
    public void IsInstalled_FalseWhenNoUserReg()
    {
        using var temp = new TempDirectory();
        var prefix = temp.Combine("empty");
        Directory.CreateDirectory(prefix);
        Assert.False(new DxvkManager().IsInstalled(prefix));
    }


    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}


/// <summary>Runner 解析：托管 / 系统 / Proton / 自定义。</summary>
public class RunnerResolverTests
{
    private static (RunnerResolver Resolver, XdgPaths Paths, TempDirectory Temp) Create()
    {
        var temp = new TempDirectory();
        var paths = new XdgPaths(temp.Combine("c"), temp.Combine("d"), temp.Combine("k"));
        var components = new ComponentRepository(paths.ComponentsDirectory);
        return (new RunnerResolver(paths, components), paths, temp);
    }


    private static string MakeRunner(XdgPaths paths, string name, string version = "wine-10.0 (test)")
    {
        var bin = Path.Join(paths.RunnersDirectory, name, "bin");
        Directory.CreateDirectory(bin);
        var wine = Path.Join(bin, "wine");
        File.WriteAllText(wine, $"#!/bin/sh\necho '{version}'\n");
        File.SetUnixFileMode(wine, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return wine;
    }


    [Fact]
    public void Managed_ResolvesToRunnersDirectory()
    {
        var (resolver, paths, temp) = Create();
        using (temp)
        {
            var wine = MakeRunner(paths, "spritz-wine-cachyos-wow64-10.0-11");
            var (resolved, dir, env, explain) = resolver.Resolve(
                new RunnerSource.Managed("spritz-wine-cachyos-wow64-10.0-11"));

            Assert.Equal(wine, resolved);
            Assert.Equal(Path.Join(paths.RunnersDirectory, "spritz-wine-cachyos-wow64-10.0-11"), dir);
            Assert.Contains("托管组件", explain);
        }
    }


    [Fact]
    public void Managed_Missing_Throws()
    {
        var (resolver, _, temp) = Create();
        using (temp)
        {
            var ex = Assert.Throws<RuntimeNotFoundException>(
                () => resolver.Resolve(new RunnerSource.Managed("nope")));
            Assert.Equal("nope", ex.RuntimeName);
        }
    }


    [Fact]
    public void Proton_ResolvesToDistBinWine_NotProtonScript()
    {
        var (resolver, _, temp) = Create();
        using (temp)
        {
            var library = temp.Combine("steam");
            var dist = Path.Join(library, "steamapps", "common", "Proton - Experimental", "dist", "bin");
            Directory.CreateDirectory(dist);
            var wine = Path.Join(dist, "wine");
            File.WriteAllText(wine, "#!/bin/sh\necho wine-9.0\n");
            File.SetUnixFileMode(wine, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var resolved = resolver.Resolve(new RunnerSource.Proton(library, "Proton - Experimental"));

            // 默认 WineBinary 模式（D9）：必须是 dist/bin/wine，而不是 proton 脚本
            Assert.Equal(wine, resolved.WineBinary);
            Assert.DoesNotContain("proton", Path.GetFileName(resolved.WineBinary));
        }
    }


    [Fact]
    public void Custom_DirectoryInput_FindsBinWine()
    {
        var (resolver, _, temp) = Create();
        using (temp)
        {
            var root = temp.Combine("mywine");
            var bin = Path.Join(root, "bin");
            Directory.CreateDirectory(bin);
            var wine = Path.Join(bin, "wine");
            File.WriteAllText(wine, "#!/bin/sh\n");
            File.SetUnixFileMode(wine, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var resolved = resolver.Resolve(new RunnerSource.Custom(root));
            Assert.Equal(wine, resolved.WineBinary);
        }
    }


    [Fact]
    public void Custom_NonExecutable_Throws()
    {
        var (resolver, _, temp) = Create();
        using (temp)
        {
            var path = temp.Combine("notexec");
            File.WriteAllText(path, "x");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Assert.Throws<RuntimeNotFoundException>(() => resolver.Resolve(new RunnerSource.Custom(path)));
        }
    }


    [Fact]
    public void Custom_Missing_Throws()
    {
        var (resolver, _, temp) = Create();
        using (temp)
        {
            Assert.Throws<RuntimeNotFoundException>(
                () => resolver.Resolve(new RunnerSource.Custom(temp.Combine("missing"))));
        }
    }


    [Fact]
    public void System_ProbeVersionReadsStdout()
    {
        using var temp = new TempDirectory();
        var wine = temp.CreateFakeWine();
        Assert.Contains("wine-10.0", RunnerResolver.ProbeVersion(wine));
    }
}


/// <summary>关键字替换 + 参数拆分在真实 profile 上的组合。</summary>
public class PlanFactoryTests
{
    private static (XdgPaths Paths, TempDirectory Temp, string Wine) Setup()
    {
        var temp = new TempDirectory();
        var paths = new XdgPaths(temp.Combine("c"), temp.Combine("d"), temp.Combine("k"));
        var bin = Path.Join(paths.RunnersDirectory, "runner", "bin");
        Directory.CreateDirectory(bin);
        var wine = Path.Join(bin, "wine");
        File.WriteAllText(wine, "#!/bin/sh\necho wine-10.0\n");
        File.SetUnixFileMode(wine, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return (paths, temp, wine);
    }


    private static LaunchPlanFactory Factory(XdgPaths paths) => new(
        new RuntimeResolver(
            new Dictionary<string, RuntimeConfig>
            {
                ["r"] = new() { Runner = new RunnerSource.Managed("runner") },
            },
            "r",
            new RunnerResolver(paths, new ComponentRepository(paths.ComponentsDirectory)),
            new ComponentRepository(paths.ComponentsDirectory),
            paths),
        paths);


    [Fact]
    public void BaselineEnv_IsPresentAndSourced()
    {
        var (paths, temp, _) = Setup();
        using (temp)
        {
            var profile = new ProfileConfig { Game = "genshin", Edition = "China", Path = temp.Path };
            var plan = Factory(paths).Build("p", profile,
                GameLaunchInfo.Fallback("genshin", "China"), dryRun: true);

            Assert.Equal("win64", plan.Env["WINEARCH"]);
            Assert.Equal(LunarWard.Wine.EnvLayer.Baseline, plan.EnvSources["WINEARCH"]);
            Assert.Equal(plan.Prefix, plan.Env["WINEPREFIX"]);
        }
    }


    [Fact]
    public void TenLayers_HaveExpectedPrecedence()
    {
        var (paths, temp, _) = Setup();
        using (temp)
        {
            var profile = new ProfileConfig
            {
                Path = temp.Path,
                Runtime = new ScopedRuntimeDto
                {
                    Kind = "PerGame",
                    Value = new RuntimeConfigDto
                    {
                        Runner = new RunnerDto { Kind = "Managed", Component = "runner" },
                        Env = new Dictionary<string, string> { ["LAYER"] = "runtime.env" },
                    },
                },
                Wine = new WineSettings { Language = "zh-cn" },
                Enhancements = new EnhancementSettings { Hud = "fps" },
                Env = new Dictionary<string, string> { ["LAYER"] = "profile.env" },
            };

            var info = GameLaunchInfo.Fallback("genshin", "China") with
            {
                GameEnv = new Dictionary<string, string> { ["LAYER"] = "game" },
            };

            var plan = Factory(paths).Build("p", profile, info, dryRun: true);

            // 层 8（游戏）> 层 7（profile.env）> 层 4（runtime.env）
            Assert.Equal("game", plan.Env["LAYER"]);
            Assert.Equal(LunarWard.Wine.EnvLayer.GameAdapter, plan.EnvSources["LAYER"]);

            // 层 5 / 层 6
            Assert.Equal("zh_CN.UTF-8", plan.Env["LANG"]);
            Assert.Equal("fps", plan.Env["DXVK_HUD"]);
        }
    }


    [Fact]
    public void DisabledExternalPrograms_AreExcluded()
    {
        var (paths, temp, _) = Setup();
        using (temp)
        {
            var profile = new ProfileConfig
            {
                Path = temp.Path,
                ExternalPrograms =
                [
                    new ExternalProgram { Id = "a", Path = "/bin/true", Enabled = true },
                    new ExternalProgram { Id = "b", Path = "/bin/true", Enabled = false },
                ],
            };

            var plan = Factory(paths).Build("p", profile, GameLaunchInfo.Fallback("genshin", "China"), dryRun: true);
            Assert.Single(plan.ExternalPrograms);
            Assert.Equal("a", plan.ExternalPrograms[0].Id);
        }
    }


    [Fact]
    public void ExternalProgram_GetsThreeStateEnv()
    {
        var (paths, temp, _) = Setup();
        using (temp)
        {
            var profile = new ProfileConfig
            {
                Path = temp.Path,
                Env = new Dictionary<string, string> { ["KEEP"] = "v", ["KILL"] = "v" },
                ExternalPrograms =
                [
                    new ExternalProgram
                    {
                        Id = "p",
                        Path = "/bin/true",
                        Env =
                        [
                            new EnvEntry { Key = "EMPTY", Kind = EnvPatchKind.Override, Value = "" },
                            new EnvEntry { Key = "KILL", Kind = EnvPatchKind.Remove },
                        ],
                    },
                ],
            };

            var plan = Factory(paths).Build("p", profile, GameLaunchInfo.Fallback("genshin", "China"), dryRun: true);
            var env = plan.ExternalPrograms[0].Env;

            Assert.Equal("v", env["KEEP"]);
            Assert.False(env.ContainsKey("KILL"));
            Assert.Equal("", env["EMPTY"]);
            // 游戏 env 本身不受外部程序补丁影响
            Assert.Equal("v", plan.Env["KILL"]);
        }
    }


    [Fact]
    public void Keywords_AreExpandedInArgumentsAndPaths()
    {
        var (paths, temp, wine) = Setup();
        using (temp)
        {
            var game = temp.Combine("game");
            Directory.CreateDirectory(game);
            var exe = Path.Join(game, "tool.exe");
            File.WriteAllText(exe, "MZ");

            var profile = new ProfileConfig
            {
                Path = game,
                Args = ["--game=%game%", "--exe=%exe%"],
                ExternalPrograms =
                [
                    new ExternalProgram
                    {
                        Id = "p",
                        Path = "%game%/tool.exe",
                        Args = ["--prefix=%prefix%"],
                    },
                ],
            };

            var plan = Factory(paths).Build("p", profile, GameLaunchInfo.Fallback("genshin", "China"), dryRun: true);

            Assert.Contains($"--game={game}", plan.Arguments);
            Assert.Contains("--exe=YuanShen.exe", plan.Arguments);
            Assert.Equal(exe, plan.ExternalPrograms[0].Executable);
            Assert.Equal($"--prefix={plan.Prefix}", plan.ExternalPrograms[0].Arguments[0]);
            Assert.Equal(game, plan.ExternalPrograms[0].WorkingDirectory);
        }
    }


    [Fact]
    public void PrefixResolver_HonoursScopes()
    {
        var (paths, temp, _) = Setup();
        using (temp)
        {
            var own = PrefixResolver.Resolve(new PrefixScope.Own(""), "my-profile", paths);
            Assert.Equal(Path.Join(paths.PrefixesDirectory, "my-profile"), own);

            var shared = PrefixResolver.Resolve(new PrefixScope.Shared("common"), "p", paths);
            Assert.Equal(Path.Join(paths.PrefixesDirectory, "common"), shared);

            var compat = PrefixResolver.Resolve(new PrefixScope.ProtonCompatData("/steam/compatdata/123"), "p", paths);
            Assert.Equal("/steam/compatdata/123/pfx", compat);
        }
    }


    [Fact]
    public void CommandOverride_IsPreservedOnPlan()
    {
        var (paths, temp, _) = Setup();
        using (temp)
        {
            var profile = new ProfileConfig
            {
                Path = temp.Path,
                CommandOverride = "gamemoderun %command%",
                Wrappers = ["gamemoderun"],
            };

            var plan = Factory(paths).Build("p", profile, GameLaunchInfo.Fallback("genshin", "China"), dryRun: true);
            Assert.Equal("gamemoderun %command%", plan.CommandTemplate);
            Assert.Equal("gamemoderun", plan.BuildArgumentVector()[0]);
            Assert.True(CommandTemplate.ContainsPost("x %post%"));
        }
    }
}
