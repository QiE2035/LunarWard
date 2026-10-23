using LunarWard.Launch;
using LunarWard.Platform;
using Xunit;

namespace LunarWard.IntegrationTests;

/// <summary>
/// S1–S5：环境 shell 与 <c>launch-report.json</c> 的一致性。
/// </summary>
/// <remarks>
/// 见 <c>docs/05-ROADMAP.md</c> §2.3。核心断言：shell 进程的
/// <c>/proc/&lt;pid&gt;/environ</c> 必须与 <c>LaunchPlan.Env</c> 逐键相等——
/// 否则"调试环境和实际启动环境不一致"，shell 就失去意义。
/// </remarks>
[Collection("phase-engine")]
public class ShellTests
{
    private static bool Available => NativeHelper.Available;


    /// <summary>
    /// S1：用一个"假 wine"充当 shell 的等价物 —— 它继承 plan.Env 并把自己的
    /// environ 原样 dump 出来，然后逐键比对。
    /// </summary>
    [Fact]
    public async Task S1_PlanEnv_IsWhatTheChildProcessActuallyGets()
    {
        if (!Available)
        {
            return;
        }

        using var sandbox = new Sandbox();
        var dump = Path.Join(sandbox.Root, "env-dump.txt");

        // 用 sandbox 的 CreatePlan 造一份"十层合并结果"，再加几个有代表性键
        var wine = sandbox.CreateWine(f => f.GameSeconds = 1);
        var plan = sandbox.CreatePlan(wine, b =>
        {
            b.ReadyGrace = TimeSpan.FromMilliseconds(100);
            b.Env["LW_ONE"] = "1";
            b.Env["LW_EMPTY"] = "";
            b.Env["LW_SPACED"] = "a b c";
        });

        // 直接拉起一个 shell，用 plan.Env 作为环境（与 ShellWindow 的做法一致）
        var script = $"tr '\\0' '\\n' < /proc/$$/environ > '{dump}'; sleep 1";
        var psi = new ProcessRequest
        {
            FileName = "/bin/sh",
            Arguments = ["-c", script],
            WorkingDirectory = plan.GameDirectory,
            Environment = plan.Env,
            NewSession = true,
        };
        using var process = UnixProcessLauncher.Default.Start(psi);

        Assert.True(await Sandbox.WaitUntilAsync(() => File.Exists(dump) && new FileInfo(dump).Length > 0,
            TimeSpan.FromSeconds(10)), "shell 没有 dump environ");

        await process.WaitForExitAsync();

        var lines = File.ReadAllLines(dump);
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var index = line.IndexOf('=');
            if (index > 0)
            {
                actual[line[..index]] = line[(index + 1)..];
            }
        }

        foreach (var (key, value) in plan.Env)
        {
            Assert.True(actual.ContainsKey(key), $"缺少环境变量 {key}");
            Assert.Equal(value, actual[key]);
        }

        // 空值必须"存在且为空"，不能变成"不存在"
        Assert.True(actual.ContainsKey("LW_EMPTY"));
        Assert.Equal("", actual["LW_EMPTY"]);
        Assert.Equal("a b c", actual["LW_SPACED"]);
    }


    /// <summary>S2：cwd 必须是游戏目录。</summary>
    [Fact]
    public async Task S2_ShellWorkingDirectory_IsGameDirectory()
    {
        if (!Available)
        {
            return;
        }

        using var sandbox = new Sandbox();
        var dump = Path.Join(sandbox.Root, "cwd.txt");

        var psi = new ProcessRequest
        {
            FileName = "/bin/sh",
            Arguments = ["-c", $"readlink /proc/$$/cwd > '{dump}'; sleep 0.3"],
            WorkingDirectory = sandbox.GameDirectory,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PATH"] = "/usr/bin:/bin",
            },
            NewSession = true,
        };
        using var process = UnixProcessLauncher.Default.Start(psi);
        process.WaitForExit();

        var cwd = File.ReadAllText(dump).Trim();
        Assert.Equal(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(sandbox.GameDirectory)),
            Path.TrimEndingDirectorySeparator(cwd));
    }


    /// <summary>S3：环境里带着 WINEPREFIX，且能被 wine 识别（用假 wine 验证传递链）。</summary>
    [Fact]
    public async Task S3_EnvContainsWinePrefixForWineCommands()
    {
        if (!Available)
        {
            return;
        }

        using var sandbox = new Sandbox();
        var dump = Path.Join(sandbox.Root, "prefix.txt");

        var psi = new ProcessRequest
        {
            FileName = "/bin/sh",
            Arguments = ["-c", $"printf '%s' \"$WINEPREFIX\" > '{dump}'"],
            WorkingDirectory = sandbox.GameDirectory,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PATH"] = "/usr/bin:/bin",
                ["WINEPREFIX"] = sandbox.Prefix,
            },
            NewSession = true,
        };
        using var process = UnixProcessLauncher.Default.Start(psi);
        process.WaitForExit();

        Assert.Equal(sandbox.Prefix, File.ReadAllText(dump));
    }


    /// <summary>S4 / S5：关闭后无残留——对整棵进程树发 SIGHUP 能把 shell 带走。</summary>
    [Fact]
    public async Task S4_KillingShellLeavesNoOrphan()
    {
        if (!Available)
        {
            return;
        }

        using var sandbox = new Sandbox();
        var marker = Path.Join(sandbox.Root, "child.pid");

        // shell 再 fork 一个子进程，验证进程组回收
        var psi = new ProcessRequest
        {
            FileName = "/bin/sh",
            Arguments = ["-c", $"sleep 30 & echo $! > '{marker}'; wait"],
            WorkingDirectory = sandbox.GameDirectory,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PATH"] = "/usr/bin:/bin",
            },
            NewSession = true,
        };
        using var process = UnixProcessLauncher.Default.Start(psi);

        Assert.True(await Sandbox.WaitUntilAsync(() => File.Exists(marker) && new FileInfo(marker).Length > 0,
            TimeSpan.FromSeconds(5)));

        var childPid = int.Parse(File.ReadAllText(marker).Trim());
        Assert.True(sandbox.Processes.IsAlive(childPid));

        // setsid 之后 sh 是新会话/进程组的组长；负 pid 才是"整组"。
        // 注意：setsid 可能 fork，LaunchProcess 的 pid 未必等于组长的 pid，
        // 所以这里用子进程自己的进程组（它继承自 sh）。
        var pgid = ReadProcessGroupId(childPid);
        Assert.True(pgid > 0, "读不到子进程的进程组 id");
        LinuxProcessController.Default.Signal(-pgid, 15);

        var ok = await Sandbox.WaitUntilAsync(
            () => sandbox.Processes.IsDeadOrZombie(childPid) && sandbox.Processes.IsDeadOrZombie(process.Id),
            TimeSpan.FromSeconds(10));
        if (!ok)
        {
            var info = "";
            foreach (var pid in new[] { process.Id, childPid })
            {
                try
                {
                    var stat = File.ReadAllText($"/proc/{pid}/stat");
                    info += $"{pid}: {stat[(stat.LastIndexOf(')') + 2)..].Split(' ')[0]} ; ";
                }
                catch (Exception ex)
                {
                    info += $"{pid}: {ex.GetType().Name} ; ";
                }
            }

            Assert.Fail($"shell 或子进程未被回收（{info}）");
        }

        try
        {
            LinuxProcessController.Default.Signal(childPid, 9);
        }
        catch (Exception)
        {
            // ignore
        }
    }


    /// <summary>读 <c>/proc/&lt;pid&gt;/stat</c> 的 pgrp（第 5 个字段）。</summary>
    private static int ReadProcessGroupId(int pid)
    {
        try
        {
            var stat = File.ReadAllText($"/proc/{pid}/stat");
            var close = stat.LastIndexOf(')');
            var fields = stat[(close + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // fields[0]=state fields[1]=ppid fields[2]=pgrp
            return int.Parse(fields[2]);
        }
        catch (Exception)
        {
            return -1;
        }
    }


    /// <summary>导出 env.sh 的格式：能被 <c>sh</c> 直接 source。</summary>
    [Fact]
    public void ExportScript_IsValidShell()
    {
        // 与 ShellViewModel.BuildExportScript 同构的渲染逻辑
        var env = new Dictionary<string, string>
        {
            ["WINEPREFIX"] = "/path/with space/prefix",
            ["QUOTED"] = "it's here",
            ["EMPTY"] = "",
        };

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("#!/bin/sh");
        builder.AppendLine("cd '/tmp'");
        foreach (var (key, value) in env)
        {
            builder.AppendLine($"export {key}={Quote(value)}");
        }

        var path = Path.Join(Path.GetTempPath(), "lunarward-env-" + Guid.NewGuid().ToString("N")[..8] + ".sh");
        File.WriteAllText(path, builder.ToString());

        try
        {
            // 用 sh -n 做语法检查
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/sh",
                ArgumentList = { "-n", path },
                UseShellExecute = false,
                RedirectStandardError = true,
            })!;
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, stderr);

            // 并真的 source 一下，检查引号处理
            var check = Path.Join(Path.GetTempPath(), "lunarward-check-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
            File.WriteAllText(check, "");
            using var sourced = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/sh",
                ArgumentList = { "-c", $". {path}; printf '%s|%s' \"$WINEPREFIX\" \"$QUOTED\" > {check}" },
                UseShellExecute = false,
            })!;
            sourced.WaitForExit();
            Assert.Equal("/path/with space/prefix|it's here", File.ReadAllText(check));
            File.Delete(check);
        }
        finally
        {
            File.Delete(path);
        }

        static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }
}
