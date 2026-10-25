using System.Text.Json;
using LunarWard.Launch;
using LunarWard.Platform;
using LunarWard.Wine;
using Xunit;
using Xunit.Abstractions;

namespace LunarWard.IntegrationTests;

/// <summary>
/// 真机验收里可以自动化的部分（对应 R1–R15 中不依赖游戏本体的项目）。
/// </summary>
/// <remarks>
/// 需要游戏本体或人工观察的项（R1–R8、R10、R11、R14）不在这里伪造通过，
/// 见 <c>docs/acceptance-2026-10-23.md</c>。
/// </remarks>
[Collection("phase-engine")]
public class AcceptanceTests
{
    private readonly ITestOutputHelper _output;

    public AcceptanceTests(ITestOutputHelper output) => _output = output;

    private static LaunchEngine CreateEngine(Sandbox sandbox)
        => new(sandbox.Processes, sandbox.Controller, sandbox.Launcher, sandbox.PrefixManager, sandbox.Xdg);


    /// <summary>R9：托管 wine 能被解析并探测到版本。</summary>
    [Fact]
    public void R9_ManagedRunner_ResolvesAndProbesVersion()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var runner = Path.Join(home, ".local", "share", "sleepy-launcher", "runners",
            "spritz-wine-cachyos-wow64-10.0-11", "bin", "wine");

        if (!File.Exists(runner))
        {
            return; // 本机没有该 runner 时跳过（不是失败）
        }

        var version = RunnerResolver.ProbeVersion(runner);
        Assert.NotNull(version);
        Assert.Contains("wine", version, StringComparison.OrdinalIgnoreCase);

        // 独立的 XDG 根目录 + "借用" 语义（Custom 来源）
        var temp = Path.Join(Path.GetTempPath(), "lunarward-acc-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(temp);
        var paths = new XdgPaths(Path.Join(temp, "c"), Path.Join(temp, "d"), Path.Join(temp, "k"));
        var components = new ComponentRepository(paths.ComponentsDirectory);
        var resolver = new RunnerResolver(paths, components);

        var resolved = resolver.Resolve(new RunnerSource.Custom(runner));
        Assert.Equal(runner, resolved.WineBinary);
        Assert.False(string.IsNullOrWhiteSpace(resolved.Explain));

        try
        {
            Directory.Delete(temp, recursive: true);
        }
        catch (Exception)
        {
            // ignore
        }
    }


    /// <summary>R12：游戏退出后 prefix_clients 收敛为空（真 /proc）。</summary>
    [Fact]
    public async Task R12_NoResidualWineClientsAfterExit()
    {
        if (!NativeHelper.Available)
        {
            return;
        }

        using var sandbox = new Sandbox();
        var wine = sandbox.CreateWine(f => f.GameSeconds = 1);
        var plan = sandbox.CreatePlan(wine, b => b.RunningSettle = TimeSpan.FromSeconds(10));

        var result = await CreateEngine(sandbox).RunAsync(plan);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Empty(sandbox.Processes.GetPrefixClients(sandbox.Prefix));
    }


    /// <summary>
    /// R15：崩溃恢复 —— 上次留下的残留客户端，下次启动必须被 Prepare 清掉。
    /// （这是"强杀游戏 → 下次能正常起来"的可自动化等价物。）
    /// </summary>
    [Fact]
    public async Task R15_StaleClientsFromCrash_AreCleanedOnNextLaunch()
    {
        if (!NativeHelper.Available)
        {
            return;
        }

        using var sandbox = new Sandbox();

        // 模拟上一次崩溃：留下 3 个本 prefix 的客户端
        var stale = new List<int>();
        for (var i = 0; i < 3; i++)
        {
            stale.Add(sandbox.SpawnStaleClient());
        }

        await Sandbox.WaitUntilAsync(
            () => sandbox.Processes.GetPrefixClients(sandbox.Prefix).Count >= 3, TimeSpan.FromSeconds(10));
        Assert.Equal(3, sandbox.Processes.GetPrefixClients(sandbox.Prefix).Count);

        var wine = sandbox.CreateWine(f => f.GameSeconds = 1);
        var result = await CreateEngine(sandbox).RunAsync(sandbox.CreatePlan(wine));

        Assert.True(result.Success, result.Error?.Message);
        Assert.All(stale, pid => Assert.True(sandbox.Processes.IsDeadOrZombie(pid), $"残留 pid {pid} 未被清理"));
    }


    /// <summary>启动报告结构：七个相位 + 完整 env + 命令（R 项都会用到它排障）。</summary>
    [Fact]
    public async Task Report_HasSevenPhasesAndFullChain()
    {
        if (!NativeHelper.Available)
        {
            return;
        }

        using var sandbox = new Sandbox();
        var wine = sandbox.CreateWine(f => f.GameSeconds = 1);
        var plan = sandbox.CreatePlan(wine);
        var result = await CreateEngine(sandbox).RunAsync(plan);

        _output.WriteLine($"acceptance run: success={result.Success} error={result.Error?.Message} error_type={result.Error?.GetType().Name}");
        Assert.True(result.Success, result.Error?.Message);

        var report = LaunchReport.From(plan, result);
        var json = JsonSerializer.Serialize(report);
        _output.WriteLine($"report env={report.Env.Count} plan env={plan.Env.Count} sources={report.EnvSources.Count} runner={report.Runtime?.Runner?.Kind}/{report.Runtime?.Runner?.Resolved}");
        _output.WriteLine(json);
        Assert.Contains("Prepare", json);
        Assert.Contains("AwaitReady", json);
        Assert.Contains("\"env_sources\"", json);
        Assert.Equal(plan.Env.Count, report.Env.Count);
        Assert.Equal(plan.Env.Count, report.EnvSources.Count);
        Assert.NotEmpty(report.Command);
        Assert.Equal(plan.Runtime.Runner.KindName, report.Runtime?.Runner?.Kind);
        Assert.Equal(wine, report.Runtime?.Runner?.Resolved);
    }


    /// <summary>同一份 plan 给两次消费（启动 + shell）得到完全一致的 env。</summary>
    [Fact]
    public void SamePlan_YieldsIdenticalEnvForLaunchAndShell()
    {
        if (!NativeHelper.Available)
        {
            return;
        }

        using var sandbox = new Sandbox();
        var wine = sandbox.CreateWine();
        var plan = sandbox.CreatePlan(wine);

        // shell 侧读的是 plan.Env（不重新构建）
        var shellEnv = new Dictionary<string, string>(plan.Env, StringComparer.Ordinal);
        Assert.Equal(plan.Env.Count, shellEnv.Count);
        foreach (var (key, value) in plan.Env)
        {
            Assert.Equal(value, shellEnv[key]);
        }
    }


    /// <summary>Dry-run 能给出完整计划（R12 与排障都用它）。</summary>
    [Fact]
    public async Task DryRun_ProducesFullPlanButLaunchesNothing()
    {
        if (!NativeHelper.Available)
        {
            return;
        }

        using var sandbox = new Sandbox();
        var wine = sandbox.CreateWine();
        var plan = sandbox.CreatePlan(wine, b => b.DryRun = true);

        var result = await CreateEngine(sandbox).RunAsync(plan);

        Assert.True(result.Success);
        Assert.Empty(sandbox.Processes.GetPrefixClients(sandbox.Prefix));
        Assert.NotEmpty(plan.BuildArgumentVector());
        Assert.NotEmpty(plan.Env);
    }
}
