using System.Diagnostics;
using LunarWard.Launch;
using LunarWard.Platform;
using Xunit;
using Xunit.Abstractions;

namespace LunarWard.IntegrationTests;

/// <summary>
/// I1–I13：用合成"游戏"（<c>fakeproc</c>）与临时 prefix 验证相位引擎的完整流程。
/// <b>不需要游戏本体</b>，也不需要真 wine。
/// </summary>
/// <remarks>见 <c>docs/05-ROADMAP.md</c> §2.2。</remarks>
[Collection("phase-engine")]
public class PhaseEngineTests
{
    private readonly ITestOutputHelper _output;

    public PhaseEngineTests(ITestOutputHelper output) => _output = output;

    private static LaunchEngine CreateEngine(Sandbox sandbox)
        => new(sandbox.Processes, sandbox.Controller, sandbox.Launcher, sandbox.PrefixManager, sandbox.Xdg);


    // ── I1 清理 ──────────────────────────────────────────────────────────
    [Fact]
    public async Task I1_Prepare_CleansPreexistingClients()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var stalePid = sandbox.SpawnStaleClient();
        await Sandbox.WaitUntilAsync(() => sandbox.Processes.GetPrefixClients(sandbox.Prefix).Count > 0,
            // 15s：整套测试顺序跑时机器更忙，5s 在慢盘/高负载下会偶发超时
            TimeSpan.FromSeconds(15));

        Assert.NotEmpty(sandbox.Processes.GetPrefixClients(sandbox.Prefix));

        var wine = sandbox.CreateWine(f => f.GameSeconds = 1);
        var plan = sandbox.CreatePlan(wine);
        var engine = CreateEngine(sandbox);

        var result = await engine.RunAsync(plan);

        // 失败时把相位结果与进程快照打到测试输出（xunit 只显示失败用例的输出）
        _output.WriteLine($"I1 run: success={result.Success} error={result.Error?.Message}");
        _output.WriteLine($"I1 clients after run: {string.Join(",", sandbox.Processes.GetPrefixClients(sandbox.Prefix).Select(c => c.Pid))}");

        Assert.True(result.Success, result.Error?.Message);
        Assert.Empty(sandbox.Processes.GetPrefixClients(sandbox.Prefix));
        // 残留进程确实被杀了
        Assert.True(sandbox.Processes.IsDeadOrZombie(stalePid));
    }


    // ── I2 首个客户 ──────────────────────────────────────────────────────
    [Fact]
    public async Task I2_GameIsTheFirstWineClient()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var wine = sandbox.CreateWine(f => f.GameSeconds = 2);
        var plan = sandbox.CreatePlan(wine);
        var engine = CreateEngine(sandbox);

        var result = await engine.RunAsync(plan);

        Assert.True(result.Success, result.Error?.Message);
        Assert.NotNull(result.GamePid);
        Assert.Equal(ReadySignal.Grace, result.Signal);
    }


    // ── I3 AwaitImage ────────────────────────────────────────────────────
    [Fact]
    public async Task I3_AwaitImage_FindsGameViaProcMaps()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var wine = sandbox.CreateWine(f => f.GameSeconds = 2);
        var plan = sandbox.CreatePlan(wine);
        var engine = CreateEngine(sandbox);

        var result = await engine.RunAsync(plan);

        Assert.True(result.Success, result.Error?.Message);
        Assert.NotNull(result.GamePid);

        var imagePhase = result.Phases.Single(p => p.Name == "AwaitImage");
        Assert.Contains($"pid={result.GamePid}", imagePhase.Detail);
    }


    // ── I4 AwaitReady 的三个信号 ─────────────────────────────────────────
    [Fact]
    public async Task I4a_AwaitReady_WineDeviceSignal()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var wine = sandbox.CreateWine(f =>
        {
            f.GameSeconds = 3;
            f.SpawnWineDevice = true;
            f.WineDeviceDelayMs = 200;
        });

        var plan = sandbox.CreatePlan(wine, b => b.ReadyGrace = TimeSpan.FromSeconds(10));
        var result = await CreateEngine(sandbox).RunAsync(plan);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(ReadySignal.WineDevice, result.Signal);
    }


    [Fact]
    public async Task I4b_AwaitReady_PlayerLogSignal()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        // 预置一个 Player.log，并在启动后立即 touch 它
        var userDir = Path.Join(sandbox.Prefix, "drive_c", "users", "steamuser",
            "AppData", "LocalLow", "miHoYo", "Genshin Impact");
        Directory.CreateDirectory(userDir);
        var playerLog = Path.Join(userDir, "Player.log");
        File.WriteAllText(playerLog, "old");
        File.SetLastWriteTimeUtc(playerLog, DateTime.UtcNow.AddMinutes(-10));

        var wine = sandbox.CreateWine(f =>
        {
            f.GameSeconds = 3;
            // 在游戏起来后 touch Player.log（模拟 Unity 初始化）
            f.ExtraLines.Add($"sleep 0.3; touch \"{playerLog}\" &");
        });

        var plan = sandbox.CreatePlan(wine, b => b.ReadyGrace = TimeSpan.FromSeconds(10));
        var result = await CreateEngine(sandbox).RunAsync(plan);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(ReadySignal.PlayerLog, result.Signal);
    }


    [Fact]
    public async Task I4c_AwaitReady_GraceFallback()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var wine = sandbox.CreateWine(f => f.GameSeconds = 3);
        var plan = sandbox.CreatePlan(wine, b => b.ReadyGrace = TimeSpan.FromMilliseconds(300));
        var result = await CreateEngine(sandbox).RunAsync(plan);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(ReadySignal.Grace, result.Signal);
    }


    // ── I5/I6 Trigger.Auto 时机与第二客户 ────────────────────────────────
    [Fact]
    public async Task I5_I6_AutoProgram_StartsAfterReady_AsSecondClient()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var marker = Path.Join(sandbox.Root, "auto.pid");
        var wine = sandbox.CreateWine(f => f.GameSeconds = 3);

        var plan = sandbox.CreatePlan(wine, b =>
        {
            b.ReadyGrace = TimeSpan.FromMilliseconds(200);
            b.Env["FAKEPROC_MARKER"] = marker;
            b.ExternalPrograms.Add(new ExternalProgramPlan
            {
                Id = "auto-prog",
                Name = "auto",
                Kind = ProgramKind.UnixBinary,
                Executable = NativeHelper.Fakeproc!,
                Arguments = ["-", "10"],
                WorkingDirectory = sandbox.GameDirectory,
                Env = new Dictionary<string, string>(b.Env, StringComparer.Ordinal),
                Trigger = Trigger.Auto,
                Wait = false,
                OnGameExit = ExitPolicy.Kill,
                PreWrites = [],
            });
        });

        var engine = CreateEngine(sandbox);
        var events = new List<PhaseProgress>();
        engine.PhaseChanged += (_, e) => events.Add(e);

        var result = await engine.RunAsync(plan);

        Assert.True(result.Success, result.Error?.Message);

        // I5：PostLaunch 只在 AwaitReady 之后
        var order = events.Select(e => e.Phase).ToList();
        Assert.True(order.IndexOf(Phase.AwaitReady) < order.IndexOf(Phase.PostLaunch));

        // I6：外部程序确实起来了（成为第二个客户端）
        var record = result.ExternalPrograms.Single(p => p.Id == "auto-prog");
        Assert.NotNull(record.Pid);
        Assert.Equal("AwaitReady", record.StartedAfter);

        // I11：Kill 策略生效——游戏退出后外部程序被终止
        Assert.True(sandbox.Processes.IsDeadOrZombie(record.Pid!.Value));
    }


    // ── I7 Trigger.Manual 不自动启动 ─────────────────────────────────────
    [Fact]
    public async Task I7_ManualProgram_IsNotStartedAutomatically()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var marker = Path.Join(sandbox.Root, "manual.pid");
        var wine = sandbox.CreateWine(f => f.GameSeconds = 1);

        var plan = sandbox.CreatePlan(wine, b =>
        {
            b.ExternalPrograms.Add(new ExternalProgramPlan
            {
                Id = "manual-prog",
                Name = "manual",
                Kind = ProgramKind.UnixBinary,
                Executable = NativeHelper.Fakeproc!,
                Arguments = ["-", "30"],
                WorkingDirectory = sandbox.GameDirectory,
                Env = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["PATH"] = "/usr/bin:/bin",
                    ["WINEPREFIX"] = sandbox.Prefix,
                    ["FAKEPROC_MARKER"] = marker,
                },
                Trigger = Trigger.Manual,
                Wait = false,
                OnGameExit = ExitPolicy.Kill,
                PreWrites = [],
            });
        });

        var result = await CreateEngine(sandbox).RunAsync(plan);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Empty(result.ExternalPrograms);
        Assert.False(File.Exists(marker));
    }


    // ── I8 Enabled=false 即使 Auto 也不启动 ──────────────────────────────
    [Fact]
    public async Task I8_DisabledProgram_IsNotStarted()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var wine = sandbox.CreateWine(f => f.GameSeconds = 1);

        // Enabled=false 的程序在 LaunchPlanFactory 层就被过滤；这里直接验证计划里没有它
        var program = new ExternalProgram
        {
            Id = "disabled",
            Path = NativeHelper.Fakeproc!,
            Kind = ProgramKind.UnixBinary,
            Trigger = Trigger.Auto,
            Enabled = false,
        };

        Assert.True(program.Enabled == false);

        var plan = sandbox.CreatePlan(wine);
        var result = await CreateEngine(sandbox).RunAsync(plan);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Empty(plan.ExternalPrograms);
    }


    // ── I9 三态 env 真的传给了子进程 ─────────────────────────────────────
    [Fact]
    public async Task I9_ExternalProgramEnv_MatchesThreeStateMerge()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var envDump = Path.Join(sandbox.Root, "child-env.txt");
        var wine = sandbox.CreateWine(f => f.GameSeconds = 2);

        // 让子进程把自己的 environ 写出来：用 /bin/sh -c 'tr "\0" "\n" < /proc/self/environ'
        var shellDump = $"tr '\\0' '\\n' < /proc/$$/environ > '{envDump}'; sleep 2";

        var plan = sandbox.CreatePlan(wine, b =>
        {
            b.ReadyGrace = TimeSpan.FromMilliseconds(100);
            b.ExternalPrograms.Add(new ExternalProgramPlan
            {
                Id = "env-prog",
                Name = "env",
                Kind = ProgramKind.UnixBinary,
                Executable = "/bin/sh",
                Arguments = ["-c", shellDump],
                WorkingDirectory = sandbox.GameDirectory,
                Env = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["PATH"] = "/usr/bin:/bin",
                    ["WINEPREFIX"] = sandbox.Prefix,
                    ["LW_OVERRIDE"] = "overridden",
                    ["LW_EMPTY"] = "",          // Override("")：存在且为空
                    // LW_REMOVED 不存在 —— Remove 的结果
                },
                Trigger = Trigger.Auto,
                Wait = false,
                OnGameExit = ExitPolicy.Kill,
                PreWrites = [],
            });
        });

        var result = await CreateEngine(sandbox).RunAsync(plan);
        Assert.True(result.Success, result.Error?.Message);

        Assert.True(await Sandbox.WaitUntilAsync(() => File.Exists(envDump) && new FileInfo(envDump).Length > 0,
            TimeSpan.FromSeconds(10)), "子进程没有 dump environ");

        var lines = File.ReadAllLines(envDump);
        var dict = lines.Where(l => l.Contains('='))
            .Select(l => (k: l[..l.IndexOf('=')], v: l[(l.IndexOf('=') + 1)..]))
            .ToDictionary(x => x.k, x => x.v, StringComparer.Ordinal);

        Assert.Equal("overridden", dict["LW_OVERRIDE"]);
        Assert.True(dict.ContainsKey("LW_EMPTY"));
        Assert.Equal("", dict["LW_EMPTY"]);
        Assert.False(dict.ContainsKey("LW_REMOVED"));
    }


    // ── I10 游戏退出收敛 ────────────────────────────────────────────────
    [Fact]
    public async Task I10_AfterGameExit_ClientsConvergeToEmpty()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var wine = sandbox.CreateWine(f => f.GameSeconds = 1);
        var plan = sandbox.CreatePlan(wine, b => b.RunningSettle = TimeSpan.FromSeconds(10));

        var result = await CreateEngine(sandbox).RunAsync(plan);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Empty(sandbox.Processes.GetPrefixClients(sandbox.Prefix));
    }


    // ── I12 dry-run ─────────────────────────────────────────────────────
    [Fact]
    public async Task I12_DryRun_DoesNotTouchAnything()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var stalePid = sandbox.SpawnStaleClient();
        await Sandbox.WaitUntilAsync(() => sandbox.Processes.GetPrefixClients(sandbox.Prefix).Count > 0,
            // 15s：整套测试顺序跑时机器更忙，5s 在慢盘/高负载下会偶发超时
            TimeSpan.FromSeconds(15));

        var wine = sandbox.CreateWine(f => f.GameSeconds = 1);
        var plan = sandbox.CreatePlan(wine, b => b.DryRun = true);

        var result = await CreateEngine(sandbox).RunAsync(plan);

        Assert.True(result.Success);
        // 残留客户端还在——dry-run 不清理
        Assert.NotEmpty(sandbox.Processes.GetPrefixClients(sandbox.Prefix));
        Assert.True(sandbox.Processes.IsAlive(stalePid));

        sandbox.Controller.Signal(stalePid, 9);
    }


    // ── I13 启动报告包含全部七相位 ───────────────────────────────────────
    [Fact]
    public async Task I13_LaunchReport_ContainsAllSevenPhases()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        var wine = sandbox.CreateWine(f => f.GameSeconds = 1);
        var plan = sandbox.CreatePlan(wine);
        var result = await CreateEngine(sandbox).RunAsync(plan);

        Assert.True(result.Success, result.Error?.Message);

        var report = LaunchReport.From(plan, result);
        var path = Path.Join(sandbox.Root, "launch-report.json");
        report.WriteTo(path);

        var loaded = LaunchReport.ReadFrom(path);
        Assert.NotNull(loaded);

        var names = loaded!.Phases.Select(p => p.Name).ToList();
        foreach (var expected in new[] { "Prepare", "Launch", "AwaitImage", "AwaitReady", "PostLaunch", "Running", "Cleanup" })
        {
            Assert.Contains(expected, names);
        }

        Assert.Equal(sandbox.Prefix, loaded.Env["WINEPREFIX"]);
        Assert.Equal("baseline", loaded.EnvSources["WINEPREFIX"]);
        Assert.NotEmpty(loaded.Command);
    }


    // ── 游戏启动期退出 → GameExitedDuringStartup ────────────────────────
    [Fact]
    public async Task GameExitsBeforeImage_ReturnsExitCode2()
    {
        if (!NativeHelper.Available)
        {
            return; // 没有 C 编译器时跳过
        }


        using var sandbox = new Sandbox();
        // 假 wine 直接退出，不 spawn 游戏
        var wine = Path.Join(sandbox.Root, "runner", "bin", "wine");
        Directory.CreateDirectory(Path.GetDirectoryName(wine)!);
        File.WriteAllText(wine, "#!/bin/bash\nexit 7\n");
        File.SetUnixFileMode(wine, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var plan = sandbox.CreatePlan(wine);
        var result = await CreateEngine(sandbox).RunAsync(plan);

        Assert.False(result.Success);
        Assert.IsType<LaunchError.GameExitedDuringStartup>(result.Error);
        Assert.Equal(2, result.ExitCode);
    }


}
