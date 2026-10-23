using System.Diagnostics;
using LunarWard.Launch;
using LunarWard.Platform;
using LunarWard.Wine;
using Xunit;

namespace LunarWard.IntegrationTests;

/// <summary>
/// 用**真实的 wine**（以及真实的 Windows PE 程序）跑完整的相位引擎。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="PhaseEngineTests"/> 的区别：那边用假 wine 脚本 + <c>fakeproc</c>，
/// 只验证引擎自身的状态机；这里用真 wine 起真 PE 进程，
/// 验证 <c>/proc/&lt;pid&gt;/environ</c> 与 <c>/proc/&lt;pid&gt;/maps</c> 的判定
/// 对真实 wine 进程成立（<c>docs/01-RESEARCH.md</c> §1.6 的核心假设）。
/// </para>
/// <para>
/// 需要：
/// </para>
/// <list type="bullet">
/// <item>可用的 wine（默认从 sleepy-launcher 借用，可用 <c>LUNARWARD_TEST_WINE</c> 覆盖）</item>
/// <item><c>x86_64-w64-mingw32-g++</c>（编译合成 PE 程序）</item>
/// <item>一个已初始化的 prefix（测试自己 <c>wineboot -u</c> 建，约 10–30 秒）</item>
/// </list>
/// <para>
/// 缺任何一项就<b>跳过</b>（返回而不是 assert），因为 CI 未必有 wine。
/// </para>
/// </remarks>
[Collection("phase-engine")]
public class RealWineTests
{
    /// <summary>
    /// 真 wine 测试默认跳过：首次要 <c>wineboot</c> 建 prefix（本机约 30–150 秒），
    /// 不适合放在常规 <c>dotnet test</c> 里。设 <c>LUNARWARD_REAL_WINE=1</c> 启用。
    /// </summary>
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("LUNARWARD_REAL_WINE") is "1" or "true" or "yes";


    private static string? FindWine()
    {
        var explicitPath = Environment.GetEnvironmentVariable("LUNARWARD_TEST_WINE");
        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
        {
            return explicitPath;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Join(home, ".local", "share", "sleepy-launcher", "runners",
                "spritz-wine-cachyos-wow64-10.0-11", "bin", "wine"),
            UnixProcessLauncher.Which("wine"),
        };

        return candidates.FirstOrDefault(p => p is not null && File.Exists(p));
    }


    private static string? BuildSyntheticGame(string outputDirectory)
    {
        var source = Path.Join(outputDirectory, "synthetic-game.cpp");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(source, """
        #include <windows.h>
        #include <cstdio>
        int main(int argc, char** argv) {
            int seconds = 3;
            for (int i = 1; i < argc; ++i) {
                // 只认 --sleep=N，避免引入 <string>
                const char* a = argv[i];
                if (a[0]=='-' && a[1]=='-' && a[2]=='s') { seconds = atoi(a + 8); }
            }
            FILE* f = fopen("synth-marker.txt", "w");
            if (f) { fprintf(f, "started pid=%lu\n", (unsigned long)GetCurrentProcessId()); fclose(f); }
            Sleep((DWORD)seconds * 1000);
            return 0;
        }
        """);

        var cc = UnixProcessLauncher.Which("x86_64-w64-mingw32-g++");
        if (cc is null)
        {
            return null;
        }

        var exe = Path.Join(outputDirectory, "SyntheticGame.exe");
        if (File.Exists(exe))
        {
            return exe;
        }

        var psi = new ProcessStartInfo
        {
            FileName = cc,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        psi.ArgumentList.Add("-O1");
        psi.ArgumentList.Add("-static");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(exe);
        psi.ArgumentList.Add(source);

        using var process = Process.Start(psi);
        process?.WaitForExit(TimeSpan.FromMinutes(2));
        return process?.ExitCode == 0 && File.Exists(exe) ? exe : null;
    }


    /// <summary>
    /// 建一个可用的 prefix —— <b>走产品代码</b>（<see cref="PrefixManager.InitializeAsync"/>），
    /// 而不是自己拼 <c>wineboot</c>。这样"建 prefix 必须禁 mono/gecko"这条也一起被测到。
    /// </summary>
    private static async Task<bool> EnsurePrefixAsync(
        string wine, string prefix, IReadOnlyDictionary<string, string> env,
        LinuxProcessTree processes, LinuxProcessController controller,
        UnixProcessLauncher launcher, IXdgPaths paths)
    {
        var manager = new PrefixManager(processes, controller, launcher, paths);
        try
        {
            await manager.InitializeAsync(wine, prefix, env);
        }
        catch (TimeoutException)
        {
            return false;
        }

        return File.Exists(Path.Join(prefix, "system.reg"));
    }


    [Fact]
    public async Task RealWine_PhaseEngine_LaunchesSyntheticPeAndFindsItViaMaps()
    {
        if (!Enabled)
        {
            return; // 默认跳过，见 Enabled 的说明
        }

        var wine = FindWine();
        if (wine is null)
        {
            return; // 没有 wine：跳过
        }

        var root = Path.Join(Path.GetTempPath(), "lunarward-realwine-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);

        try
        {
            var gameDirectory = Path.Join(root, "game");
            Directory.CreateDirectory(gameDirectory);
            var exe = BuildSyntheticGame(Path.Join(root, "synth"));
            if (exe is null)
            {
                return; // 没有 mingw：跳过
            }

            File.Copy(exe, Path.Join(gameDirectory, "SyntheticGame.exe"), overwrite: true);

            var prefix = Path.Join(root, "prefix");
            var paths = new XdgPaths(Path.Join(root, "c"), Path.Join(root, "d"), Path.Join(root, "k"));
            paths.EnsureDirectories();

            var baseEnv = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin",
                ["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ["WINEPREFIX"] = prefix,
                ["WINEDEBUG"] = "-all",
                ["WINEDLLOVERRIDES"] = "winemenubuilder.exe=d",
            };

            var processes = new LinuxProcessTree();
            var controller = new LinuxProcessController();
            var launcher = new UnixProcessLauncher();

            var sw = Stopwatch.StartNew();
            if (!await EnsurePrefixAsync(wine, prefix, baseEnv, processes, controller, launcher, paths))
            {
                return; // prefix 建不起来：跳过（不是引擎的问题）
            }

            // 冷 prefix 必须在 30 秒内建好——超过说明 mono/gecko 提示没被禁掉（曾实测 2 分 32 秒）
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30),
                $"冷 prefix 初始化用了 {sw.Elapsed.TotalSeconds:0.0} 秒，疑似卡在 wine-mono/wine-gecko 安装提示上");

            var prefixManager = new PrefixManager(processes, controller, launcher, paths);

            var plan = new LaunchPlan
            {
                ProfileId = "real-wine",
                GameKind = "genshin",
                GameDirectory = gameDirectory,
                // 真实的 PE 程序名：maps 里会出现它（wine 把 PE 映射成匿名段 + 文件名）
                GameImage = "SyntheticGame.exe",
                Runtime = new ResolvedRuntime
                {
                    Name = "real",
                    Runner = new RunnerSource.Custom(wine),
                    WineBinary = wine,
                    RunnerDirectory = Path.GetDirectoryName(wine) ?? "",
                    Dxvk = new DxvkSource.System(),
                    Explain = "test → real wine",
                },
                Prefix = prefix,
                WineBinary = wine,
                Arguments = ["SyntheticGame.exe", "--sleep=2"],
                Env = baseEnv,
                EnvSources = baseEnv.Keys.ToDictionary(k => k, _ => EnvLayer.Baseline, StringComparer.Ordinal),
                ExternalPrograms = [],
                Keywords = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["game"] = gameDirectory,
                    ["prefix"] = prefix,
                },
                WaitImageTimeout = TimeSpan.FromSeconds(90),
                ReadyGrace = TimeSpan.FromSeconds(3),
                RunningSettle = TimeSpan.FromSeconds(20),
            };

            var engine = new LaunchEngine(processes, controller, launcher, prefixManager, paths);
            var result = await engine.RunAsync(plan);

            // 最有价值的断言：真 wine 下 AwaitImage 能靠 /proc/<pid>/maps 找到 PE 进程
            Assert.True(result.Success, result.Error?.Message);

            var imagePhase = result.Phases.Single(p => p.Name == "AwaitImage");
            Assert.NotNull(result.GamePid);
            Assert.Contains("pid=", imagePhase.Detail);

            // 真 wine 下 AwaitReady 应该拿到 WineDevice 或 PlayerLog，而不是靠 grace 兜底
            Assert.NotNull(result.Signal);

            // 把完整链路打到测试输出，作为验收证据（不是断言，是记录）
            Console.WriteLine("=== launch-report (real wine) ===");
            Console.WriteLine($"signal = {result.Signal}");
            Console.WriteLine($"gamePid = {result.GamePid}");
            foreach (var phase in result.Phases)
            {
                Console.WriteLine($"  {phase.Name,-12} {phase.DurationMs,6} ms  {phase.Detail}");
            }

            // 退出后 prefix 收敛
            Assert.Empty(processes.GetPrefixClients(prefix));

            // 合成程序确实跑过（它自己写的 marker）
            Assert.True(File.Exists(Path.Join(gameDirectory, "synth-marker.txt")));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (Exception)
            {
                // best effort
            }
        }
    }


    [Fact]
    public async Task RealWine_PrefixClients_DetectsRealWineProcesses()
    {
        if (!Enabled)
        {
            return;
        }

        var wine = FindWine();
        if (wine is null)
        {
            return;
        }

        var root = Path.Join(Path.GetTempPath(), "lunarward-realwine-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);

        try
        {
            var prefix = Path.Join(root, "prefix");
            var paths = new XdgPaths(Path.Join(root, "c"), Path.Join(root, "d"), Path.Join(root, "k"));
            paths.EnsureDirectories();
            var env = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin",
                ["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ["WINEPREFIX"] = prefix,
                ["WINEDEBUG"] = "-all",
            };

            var processes = new LinuxProcessTree();
            var controller = new LinuxProcessController();
            var launcher = new UnixProcessLauncher();
            if (!await EnsurePrefixAsync(wine, prefix, env, processes, controller, launcher, paths))
            {
                return;
            }


            // wineboot 之后，prefix 里至少有一个 wineserver 客户端（或者是 0 个，取决于是否立即退出）
            var clients = processes.GetPrefixClients(prefix);

            // 关键断言：所有这些进程的 WINEPREFIX 都必须精确等于我们的 prefix
            foreach (var client in clients)
            {
                Assert.Equal(prefix, client.WinePrefix);
            }

            // 而且不能把别的 prefix 混进来
            Assert.DoesNotContain(clients, c => c.WinePrefix is not null && c.WinePrefix != prefix);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (Exception)
            {
                // best effort
            }
        }
    }
}
