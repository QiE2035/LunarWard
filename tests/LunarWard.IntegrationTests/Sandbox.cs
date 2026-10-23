using LunarWard.Launch;
using LunarWard.Platform;
using LunarWard.Wine;

namespace LunarWard.IntegrationTests;

/// <summary>
/// 集成测试的公共搭台：临时目录 + 假 wine + 临时 prefix + 真实
/// <see cref="LinuxProcessTree"/>（读真 /proc，不是 mock）。
/// </summary>
public sealed class Sandbox : IDisposable
{
    private readonly Dictionary<string, string> _envBackup = new();


    public Sandbox()
    {
        Root = Path.Join(Path.GetTempPath(), "lunarward-it-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Root);

        GameDirectory = Path.Join(Root, "game");
        Directory.CreateDirectory(GameDirectory);
        Prefix = Path.Join(Root, "prefix");
        Directory.CreateDirectory(Prefix);
        File.WriteAllText(Path.Join(Prefix, "system.reg"), "WINE REGISTRY Version 2\n");
        File.WriteAllText(Path.Join(Prefix, "user.reg"), "WINE REGISTRY Version 2\n");

        Xdg = new XdgPaths(Path.Join(Root, "config"), Path.Join(Root, "data"), Path.Join(Root, "cache"));
        Xdg.EnsureDirectories();

        // 合成"游戏 exe"——只要是个文件，fakeproc 会把它 mmap 进 maps
        GameImage = "SyntheticGame.exe";
        File.WriteAllText(Path.Join(GameDirectory, GameImage), "MZ fake pe");

        Processes = new LinuxProcessTree();
        Controller = new LinuxProcessController();
        Launcher = new UnixProcessLauncher();
        PrefixManager = new PrefixManager(Processes, Controller, Launcher, Xdg);
    }


    public string Root { get; }

    public string GameDirectory { get; }

    public string Prefix { get; }

    public string GameImage { get; }

    public XdgPaths Xdg { get; }

    public LinuxProcessTree Processes { get; }

    public LinuxProcessController Controller { get; }

    public UnixProcessLauncher Launcher { get; }

    public PrefixManager PrefixManager { get; }


    /// <summary>构造假 wine 并返回其路径。</summary>
    public string CreateWine(Action<FakeWine>? configure = null)
    {
        var fakeproc = NativeHelper.Fakeproc ?? throw new InvalidOperationException("fakeproc 编译失败");
        var device = NativeHelper.WineDevice ?? fakeproc;

        var fake = new FakeWine(fakeproc);
        configure?.Invoke(fake);

        var wine = Path.Join(Root, "runner", "bin", "wine");
        fake.Build(wine, device);

        // 紧邻的 wineserver（脚本）
        var wineserver = Path.Join(Root, "runner", "bin", "wineserver");
        File.WriteAllText(wineserver, "#!/bin/bash\nexit 0\n");
        File.SetUnixFileMode(wineserver,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return wine;
    }


    /// <summary>
    /// 造一个 <see cref="LaunchPlan"/>。默认 <c>ReadyGrace</c> 很短，
    /// 但测试需要时可以调大。
    /// </summary>
    public LaunchPlan CreatePlan(
        string wineBinary,
        Action<LaunchPlanBuilder>? configure = null)
    {
        var builder = new LaunchPlanBuilder
        {
            ProfileId = "test-profile",
            GameKind = "genshin",
            GameDirectory = GameDirectory,
            GameImage = GameImage,
            Prefix = Prefix,
            WineBinary = wineBinary,
        };
        configure?.Invoke(builder);

        return builder.Build();
    }


    /// <summary>直接拉起一个"既有客户端"（模拟上次崩溃留下的残留）。</summary>
    public int SpawnStaleClient()
    {
        var fakeproc = NativeHelper.Fakeproc!;
        var process = StartProcess(new ProcessRequest
        {
            FileName = fakeproc,
            Arguments = ["-", "30"],
            Environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WINEPREFIX"] = Prefix,
                ["PATH"] = "/usr/bin:/bin",
                ["FAKEPROC_MARKER"] = Path.Join(Root, "stale.pid"),
            },
            NewSession = true,
        });
        return process.Id;
    }


    private static System.Diagnostics.Process StartProcess(ProcessRequest request)
        => UnixProcessLauncher.Default.Start(request);


    /// <summary>等某个条件成立。</summary>
    public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout, int pollMs = 50)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(pollMs).ConfigureAwait(false);
        }

        return condition();
    }


    public void Dispose()
    {
        // 清掉本 sandbox 的残留客户端
        foreach (var client in Processes.GetPrefixClients(Prefix))
        {
            try
            {
                Controller.Signal(client.Pid, 9);
            }
            catch (Exception)
            {
                // ignore
            }
        }

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception)
        {
            // best effort
        }
    }
}


/// <summary><see cref="LaunchPlan"/> 的构造器（记录型 API 不方便逐个 init）。</summary>
public sealed class LaunchPlanBuilder
{
    public string ProfileId { get; set; } = "test";

    public string GameKind { get; set; } = "genshin";

    public string GameDirectory { get; set; } = "";

    public string GameImage { get; set; } = "SyntheticGame.exe";

    public string Prefix { get; set; } = "";

    public string WineBinary { get; set; } = "";

    public Dictionary<string, string> Env { get; set; } = new(StringComparer.Ordinal);

    public List<ExternalProgramPlan> ExternalPrograms { get; } = [];

    public TimeSpan WaitImageTimeout { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan ReadyGrace { get; set; } = TimeSpan.FromMilliseconds(800);

    public TimeSpan RunningSettle { get; set; } = TimeSpan.FromSeconds(5);

    public bool DryRun { get; set; }


    public LaunchPlan Build()
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin:/bin:/usr/local/bin",
            ["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ["WINEPREFIX"] = Prefix,
        };
        if (!env.ContainsKey("LUNARWARD_FAKEPROC"))
        {
            env["LUNARWARD_FAKEPROC"] = NativeHelper.Fakeproc ?? "/bin/true";
        }

        foreach (var (k, v) in Env)
        {
            env[k] = v;
        }

        return new LaunchPlan
        {
            ProfileId = ProfileId,
            GameKind = GameKind,
            GameDirectory = GameDirectory,
            GameImage = GameImage,
            Runtime = new ResolvedRuntime
            {
                Name = "test",
                Runner = new RunnerSource.Custom(WineBinary),
                WineBinary = WineBinary,
                RunnerDirectory = Path.GetDirectoryName(WineBinary) ?? "",
                Dxvk = new DxvkSource.System(),
                Explain = "test → fake wine",
            },
            Prefix = Prefix,
            WineBinary = WineBinary,
            Arguments = [GameImage],
            Env = env,
            EnvSources = env.Keys.ToDictionary(k => k, _ => EnvLayer.Baseline, StringComparer.Ordinal),
            ExternalPrograms = ExternalPrograms,
            Keywords = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["game"] = GameDirectory,
                ["prefix"] = Prefix,
                ["exe"] = GameImage,
                ["launcher"] = "/tmp",
                ["temp"] = "/tmp",
                ["build"] = Path.GetDirectoryName(WineBinary),
            },
            WaitImageTimeout = WaitImageTimeout,
            ReadyGrace = ReadyGrace,
            RunningSettle = RunningSettle,
            DryRun = DryRun,
        };
    }
}
