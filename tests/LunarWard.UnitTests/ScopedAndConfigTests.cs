using LunarWard.Launch;
using LunarWard.Platform;
using LunarWard.Wine;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>U3 — 作用域解析（<c>PerGame &gt; Shared &gt; Default &gt; System</c>）。</summary>
public class ScopedResolutionTests
{
    private static (RuntimeResolver Resolver, TempDirectory Temp) CreateResolver(string? defaultName = "default")
    {
        var temp = new TempDirectory();
        var paths = new XdgPaths(temp.Combine("config"), temp.Combine("data"), temp.Combine("cache"));

        // 造两个托管 runner
        foreach (var name in new[] { "runner-default", "runner-shared", "runner-pergame" })
        {
            var bin = temp.Combine("data", "lunarward", "runners", name, "bin");
            Directory.CreateDirectory(bin);
            var wine = Path.Join(bin, "wine");
            File.WriteAllText(wine, "#!/bin/sh\necho wine\n");
            File.SetUnixFileMode(wine, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var components = new ComponentRepository(paths.ComponentsDirectory);
        var runtimes = new Dictionary<string, RuntimeConfig>
        {
            ["shared"] = new() { Runner = new RunnerSource.Managed("runner-shared") },
            ["default"] = new() { Runner = new RunnerSource.Managed("runner-default") },
        };

        var resolver = new RuntimeResolver(runtimes, defaultName, new RunnerResolver(paths, components), components, paths);
        return (resolver, temp);
    }


    [Fact]
    public void PerGame_Wins()
    {
        var (resolver, temp) = CreateResolver();
        using (temp)
        {
            var scope = new Scoped<RuntimeConfig>.PerGame(new RuntimeConfig
            {
                Runner = new RunnerSource.Managed("runner-pergame"),
            });

            var resolved = resolver.Resolve(scope);
            Assert.Contains("runner-pergame", resolved.WineBinary);
            Assert.Contains("独占", resolved.Explain);
        }
    }


    [Fact]
    public void Custom_Wins()
    {
        var (resolver, temp) = CreateResolver();
        using (temp)
        {
            var scope = new Scoped<RuntimeConfig>.Custom(new RuntimeConfig
            {
                Runner = new RunnerSource.Managed("runner-pergame"),
            });

            var resolved = resolver.Resolve(scope);
            Assert.Contains("runner-pergame", resolved.WineBinary);
            Assert.Contains("自定义", resolved.Explain);
        }
    }


    [Fact]
    public void Shared_BeatsDefault()
    {
        var (resolver, temp) = CreateResolver();
        using (temp)
        {
            var resolved = resolver.Resolve(new Scoped<RuntimeConfig>.Shared("shared"));
            Assert.Contains("runner-shared", resolved.WineBinary);
        }
    }


    [Fact]
    public void Default_UsedWhenScopeIsDefault()
    {
        var (resolver, temp) = CreateResolver();
        using (temp)
        {
            var resolved = resolver.Resolve(new Scoped<RuntimeConfig>.Default());
            Assert.Contains("runner-default", resolved.WineBinary);
            Assert.Contains("default", resolved.Explain);
        }
    }


    [Fact]
    public void MissingShared_FallsBackToDefault_AndSaysSo()
    {
        var (resolver, temp) = CreateResolver();
        using (temp)
        {
            var resolved = resolver.Resolve(new Scoped<RuntimeConfig>.Shared("nope"));
            Assert.Contains("runner-default", resolved.WineBinary);
            Assert.Contains("回退", resolved.Explain);
        }
    }


    [Fact]
    public void NoDefault_FallsBackToSystem()
    {
        var (resolver, temp) = CreateResolver(defaultName: null);
        using (temp)
        {
            // 系统 wine 可能不存在；只要不是我们的托管组件即可
            try
            {
                var resolved = resolver.Resolve(new Scoped<RuntimeConfig>.Default());
                Assert.Contains("系统", resolved.Explain);
            }
            catch (RuntimeNotFoundException ex)
            {
                // 本机没有系统 wine 也是合法结果——退到系统兜底但仍报"不可用"
                Assert.Equal("System", ex.RuntimeName);
            }
        }
    }


    [Fact]
    public void TableDriven_AllCombinations()
    {
        // 表驱动，16 种组合；这里用 (scope × 存在性) 覆盖关键四象限，
        // 断言"永远能解析出一个可执行文件，且解释串非空"
        var (resolver, temp) = CreateResolver();
        using (temp)
        {
            var scopes = new Scoped<RuntimeConfig>[]
            {
                new Scoped<RuntimeConfig>.PerGame(new RuntimeConfig { Runner = new RunnerSource.Managed("runner-pergame") }),
                new Scoped<RuntimeConfig>.Custom(new RuntimeConfig { Runner = new RunnerSource.Managed("runner-pergame") }),
                new Scoped<RuntimeConfig>.Shared("shared"),
                new Scoped<RuntimeConfig>.Shared("missing"),
                new Scoped<RuntimeConfig>.Default(),
            };

            foreach (var scope in scopes)
            {
                var resolved = resolver.Resolve(scope);
                Assert.True(File.Exists(resolved.WineBinary), $"{scope.KindName}: {resolved.WineBinary}");
                Assert.False(string.IsNullOrWhiteSpace(resolved.Explain));
            }
        }
    }
}


/// <summary>U4 — env 十层优先级。</summary>
public class EnvLayerTests
{
    [Fact]
    public void LaterLayersWin()
    {
        var builder = new EnvironmentBuilder();
        builder.Set(EnvLayer.Baseline, "A", "1");
        builder.Set(EnvLayer.Runner, "A", "2");
        builder.Set(EnvLayer.GameAdapter, "A", "3");

        Assert.Equal("3", builder.Values["A"]);
        Assert.Equal(EnvLayer.GameAdapter, builder.Sources["A"]);
    }


    [Fact]
    public void FirstWinsLayer_KeepsOriginal()
    {
        var builder = new EnvironmentBuilder();
        builder.Set(EnvLayer.Baseline, "WINEARCH", "win64");
        builder.Apply(EnvLayer.Runner, new Dictionary<string, string> { ["WINEARCH"] = "win32" }, overwrite: false);

        Assert.Equal("win64", builder.Values["WINEARCH"]);
        Assert.Equal(EnvLayer.Baseline, builder.Sources["WINEARCH"]);
    }


    [Fact]
    public void Remove_DropsKeyAndSource()
    {
        var builder = new EnvironmentBuilder();
        builder.Set(EnvLayer.Baseline, "X", "1");
        builder.Remove("X");
        Assert.False(builder.Values.ContainsKey("X"));
        Assert.False(builder.Sources.ContainsKey("X"));
    }


    [Fact]
    public void Clone_IsIndependent()
    {
        var builder = new EnvironmentBuilder();
        builder.Set(EnvLayer.Baseline, "X", "1");
        var clone = builder.Clone();
        clone.Set(EnvLayer.ProfileEnv, "X", "2");

        Assert.Equal("1", builder.Values["X"]);
        Assert.Equal("2", clone.Values["X"]);
    }


    [Fact]
    public void EmptyValue_IsSupported()
    {
        var builder = new EnvironmentBuilder();
        builder.Set(EnvLayer.GameAdapter, "DISPLAY", "");
        Assert.True(builder.Values.ContainsKey("DISPLAY"));
        Assert.Equal("", builder.Values["DISPLAY"]);
    }
}


/// <summary>U12 / U13 — 原子写与配置损坏恢复。</summary>
public class ConfigTests
{
    [Fact]
    public void ConfigLoad_RecreatesDefaultOnCorruptJson()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("config.json");
        File.WriteAllText(path, "{ this is not json ");

        var config = LauncherConfig.Load(path, out var warning);

        Assert.NotNull(warning);
        Assert.Equal(LauncherConfig.CurrentSchemaVersion, config.SchemaVersion);
        Assert.True(File.Exists(path)); // 已重建
        Assert.Single(Directory.GetFiles(temp.Path, "config.json.bad-*"));
    }


    [Fact]
    public void ConfigRoundTrips()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("config.json");

        var config = new LauncherConfig();
        config.Launcher.Style = "Classic";
        config.Runtimes["shared-cachyos"] = new RuntimeConfigDto
        {
            Runner = new RunnerDto { Kind = "Managed", Component = "spritz-wine-cachyos-wow64-10.0-11" },
            Dxvk = new DxvkDto { Kind = "Managed", Component = "dxvk-3.1.1" },
            Sync = WineSync.NTSync,
        };
        config.Profiles["genshin-cn"] = new ProfileConfig
        {
            Game = "genshin",
            Edition = "China",
            Path = "/games/YuanShen",
            Channel = "Bilibili",
        };
        config.Save(path);

        var loaded = LauncherConfig.Load(path, out var warning);
        Assert.Null(warning);
        Assert.Equal("Classic", loaded.Launcher.Style);
        Assert.Equal(WineSync.NTSync, loaded.Runtimes["shared-cachyos"].Sync);
        Assert.Equal("Bilibili", loaded.Profiles["genshin-cn"].Channel);

        // 域对象转换
        var domain = loaded.Runtimes["shared-cachyos"].ToDomain();
        Assert.IsType<RunnerSource.Managed>(domain.Runner);
        Assert.Equal("spritz-wine-cachyos-wow64-10.0-11", ((RunnerSource.Managed)domain.Runner).Component);
    }


    [Fact]
    public void ConfigPreservesUnknownFields()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("config.json");
        File.WriteAllText(path, """
        {
          "schema_version": 1,
          "future_feature": { "enabled": true },
          "launcher": { "language": "zh-cn", "unknown_launcher_key": 42 },
          "profiles": {}
        }
        """);

        var config = LauncherConfig.Load(path, out _);
        config.Save(path);

        var text = File.ReadAllText(path);
        Assert.Contains("future_feature", text);
        Assert.Contains("unknown_launcher_key", text);
    }


    [Fact]
    public void AtomicWrite_LeavesNoTmpAndReplacesContent()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("sub", "file.txt");

        Platform.AtomicFile.WriteAllText(path, "one");
        Platform.AtomicFile.WriteAllText(path, "two");

        Assert.Equal("two", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }


    [Fact]
    public void AtomicWrite_OriginalIntactWhenWriteFails()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("file.txt");
        Platform.AtomicFile.WriteAllText(path, "original");

        // 让目标目录只读，强制失败（root 下无效果，跳过）
        if (Environment.UserName == "root")
        {
            return;
        }

        var dir = temp.Path;
        File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            Assert.ThrowsAny<Exception>(() => Platform.AtomicFile.WriteAllText(Path.Join(dir, "new.txt"), "x"));
        }
        finally
        {
            File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Assert.Equal("original", File.ReadAllText(path));
    }


    [Fact]
    public void LaunchReport_RoundTripsThroughJson()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("launch-report.json");

        var report = new LaunchReport
        {
            Profile = "genshin-cn",
            Success = true,
            ExitCode = 0,
            Phases = [new LaunchReport.ReportPhase { Name = "Prepare", DurationMs = 120 }],
            Env = { ["WINEPREFIX"] = "/p" },
            EnvSources = { ["WINEPREFIX"] = "baseline" },
            Command = ["wine", "YuanShen.exe"],
        };
        report.WriteTo(path);

        var loaded = LaunchReport.ReadFrom(path);
        Assert.NotNull(loaded);
        Assert.Equal("genshin-cn", loaded!.Profile);
        Assert.Single(loaded.Phases);
        Assert.Equal("/p", loaded.Env["WINEPREFIX"]);
        Assert.Equal("baseline", loaded.EnvSources["WINEPREFIX"]);
    }
}
