using System.Diagnostics;
using LunarWard.Platform;
using LunarWard.Wine;

namespace LunarWard.Launch;

/// <summary>
/// 相位事件的快照（UI 订阅后驱动启动按钮状态机）。
/// </summary>
public sealed record PhaseProgress(Phase Phase, string Description, int? GamePid = null, ReadySignal? Signal = null);


/// <summary>
/// 外部程序运行中的句柄。
/// </summary>
public sealed class ExternalProgramHandle : IDisposable
{
    private readonly ExternalProgramPlan _plan;

    private readonly Process? _process;

    private readonly IProcessController _controller;

    private readonly List<IDisposable> _subscriptions = [];


    public ExternalProgramHandle(ExternalProgramPlan plan, Process? process, IProcessController controller)
    {
        _plan = plan;
        _process = process;
        _controller = controller;
        Pid = process?.Id;
    }


    public string Id => _plan.Id;

    public int? Pid { get; }

    public ExternalProgramPlan Plan => _plan;

    public bool HasExited
    {
        get
        {
            try
            {
                return _process is null || _process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }

    public int? ExitCode
    {
        get
        {
            try
            {
                return _process?.HasExited == true ? _process.ExitCode : null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }


    /// <summary>
    /// 按 <see cref="ExitPolicy"/> 处理。注入器这类程序会 <c>WaitForSingleObject</c>
    /// 等游戏退出，所以它可能比游戏晚一点点退出——必须先检查 <c>HasExited</c>。
    /// </summary>
    public async Task ApplyExitPolicyAsync(TimeSpan grace, CancellationToken ct = default)
    {
        if (_process is null)
        {
            return;
        }

        if (HasExited)
        {
            return;
        }

        if (_plan.OnGameExit == ExitPolicy.Keep)
        {
            return;
        }

        // 先给一点时间让它自己退出（Kill 策略也要处理"已经自己退了"的情况）
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < grace)
        {
            if (HasExited)
            {
                return;
            }

            await Task.Delay(100, ct).ConfigureAwait(false);
        }

        if (Pid is { } pid)
        {
            await _controller.TerminateAsync(pid, TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
    }


    public void Dispose()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }

        _subscriptions.Clear();
        _process?.Dispose();
    }
}


/// <summary>
/// 相位化启动引擎。本项目最核心的技术。
/// </summary>
/// <remarks>
/// <para><b>不变式</b>：游戏必须是该 wine prefix 的第一个 wine 客户端。</para>
/// <para>
/// 任何 <see cref="Trigger.Auto"/> 的外部程序<b>只能</b>在 <c>AwaitReady</c> 之后拉起；
/// 不提供"游戏启动前运行程序"的能力。见 <c>docs/03-LAUNCH-ENGINE.md</c> §1。
/// </para>
/// </remarks>
public sealed class LaunchEngine
{
    private readonly IProcessTree _processTree;

    private readonly IProcessController _controller;

    private readonly IProcessLauncher _launcher;

    private readonly IPrefixManager _prefixManager;

    private readonly IXdgPaths _paths;

    private readonly IDxvkManager? _dxvk;


    public LaunchEngine(
        IProcessTree processTree,
        IProcessController controller,
        IProcessLauncher launcher,
        IPrefixManager prefixManager,
        IXdgPaths paths,
        IDxvkManager? dxvk = null)
    {
        _processTree = processTree;
        _controller = controller;
        _launcher = launcher;
        _prefixManager = prefixManager;
        _paths = paths;
        _dxvk = dxvk;
    }


    /// <summary>相位变化通知。</summary>
    public event EventHandler<PhaseProgress>? PhaseChanged;


    /// <summary>游戏日志上限（8 MiB，沿用 Starward 的 <c>GAME_LOG_FILE_LIMIT</c>）。</summary>
    public long GameLogLimitBytes { get; init; } = 8 * 1024 * 1024;


    /// <summary>
    /// 执行完整启动流程。取消令牌只影响"等待"阶段，不会强杀游戏。
    /// </summary>
    public async Task<LaunchResult> RunAsync(LaunchPlan plan, CancellationToken ct = default)
    {
        var startedAt = DateTimeOffset.Now;
        var phases = new List<PhaseRecord>();
        var externalRecords = new List<ExternalProgramRecord>();
        var phaseSw = new Stopwatch();
        var gamePid = default(int?);
        ReadySignal? signal = null;

        void MarkPhase(string name, long ms, string? detail = null)
            => phases.Add(new PhaseRecord(name, DateTimeOffset.Now.AddMilliseconds(-ms), ms) { Detail = detail });

        void Report(Phase phase, string description, int? pid = null, ReadySignal? sig = null)
        {
            PhaseChanged?.Invoke(this, new PhaseProgress(phase, description, pid, sig));
        }

        // ─────────────── Prepare ───────────────
        Report(Phase.Prepare, "准备中…");
        phaseSw.Restart();
        try
        {
            if (!Directory.Exists(plan.GameDirectory))
            {
                MarkPhase("Prepare", phaseSw.ElapsedMilliseconds);
                return LaunchResult.Fail(new LaunchError.GameNotInstalled(plan.GameDirectory));
            }

            if (string.IsNullOrEmpty(plan.Prefix))
            {
                MarkPhase("Prepare", phaseSw.ElapsedMilliseconds);
                return LaunchResult.Fail(new LaunchError.Preflight("prefix 为空"));
            }

            if (plan.DryRun)
            {
                MarkPhase("Prepare", phaseSw.ElapsedMilliseconds, "dry-run");
                Report(Phase.Cleanup, "dry-run 完成");
                MarkPhase("Cleanup", 0, "dry-run");
                return new LaunchResult
                {
                    Success = true,
                    Phases = phases,
                    StartedAt = startedAt,
                    ExitedAt = DateTimeOffset.Now,
                };
            }

            // 1. 清理 prefix，并确认清零
            var cleaned = await _prefixManager.CleanAsync(plan.Prefix, plan.WineBinary, ct).ConfigureAwait(false);
            if (!cleaned)
            {
                var pids = _processTree.GetPrefixClients(plan.Prefix).Select(x => x.Pid).ToList();
                MarkPhase("Prepare", phaseSw.ElapsedMilliseconds, "prefix 未清空");
                return LaunchResult.Fail(new LaunchError.PrefixNotClean(pids));
            }

            // 2. DXVK（若运行时指定了托管/自定义来源）
            if (_dxvk is not null && plan.Runtime.DxvkDirectory is { Length: > 0 } dxvkDir && Directory.Exists(dxvkDir))
            {
                try
                {
                    if (!_dxvk.IsInstalled(plan.Prefix))
                    {
                        await _dxvk.InstallAsync(plan.Prefix, dxvkDir, ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    // DXVK 安装失败不阻止启动（wine 自带 d3d 仍可跑）
                    phases.Add(new PhaseRecord("Dxvk", DateTimeOffset.Now, 0) { Detail = ex.Message });
                }
            }

            // 3. 写 PreWrites（Prepare 时机）
            foreach (var program in plan.ExternalPrograms)
            {
                foreach (var write in program.PreWrites.Where(w => w.When == FileWriteWhen.Prepare))
                {
                    WriteFileSpec(write, plan.Keywords);
                }
            }

            MarkPhase("Prepare", phaseSw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            MarkPhase("Prepare", phaseSw.ElapsedMilliseconds, "canceled");
            return new LaunchResult { Success = false, Phases = phases, StartedAt = startedAt, ExitedAt = DateTimeOffset.Now };
        }
        catch (Exception ex)
        {
            MarkPhase("Prepare", phaseSw.ElapsedMilliseconds, ex.Message);
            return LaunchResult.Fail(new LaunchError.Preflight(ex.Message));
        }

        // ─────────────── Launch ───────────────
        Report(Phase.Launch, "启动中…");
        phaseSw.Restart();
        Process child;
        try
        {
            var request = new ProcessRequest
            {
                FileName = plan.WineBinary,
                Arguments = plan.Arguments,
                WorkingDirectory = plan.GameDirectory,
                Environment = plan.Env,
                RedirectOutput = true,
                RedirectError = true,
                NewSession = true,
            };

            child = _launcher.Start(request);
            AttachLogPump(child, _paths.GameLogFile);
            MarkPhase("Launch", phaseSw.ElapsedMilliseconds, string.Join(' ', plan.BuildArgumentVector().Select(Quote)));
        }
        catch (Exception ex)
        {
            MarkPhase("Launch", phaseSw.ElapsedMilliseconds, ex.Message);
            return LaunchResult.Fail(new LaunchError.Preflight($"启动 wine 失败：{ex.Message}"));
        }

        using (child)
        {
            var imageEpoch = DateTimeOffset.Now;

            // ─────────────── AwaitImage ───────────────
            Report(Phase.AwaitImage, "等待游戏…");
            phaseSw.Restart();
            var imageDeadline = imageEpoch + plan.WaitImageTimeout;
            while (true)
            {
                if (child.HasExited)
                {
                    MarkPhase("AwaitImage", phaseSw.ElapsedMilliseconds, "子进程已退出");
                    await SafeCleanupAsync(plan, ct).ConfigureAwait(false);
                    MarkPhase("Cleanup", 0);
                    return new LaunchResult
                    {
                        Success = false,
                        Error = new LaunchError.GameExitedDuringStartup(child.ExitCode, _paths.GameLogFile,
                            "子进程在游戏镜像被映射前就退出了。"),
                        Phases = phases,
                        StartedAt = startedAt,
                        ExitedAt = DateTimeOffset.Now,
                        GameExitCode = child.ExitCode,
                    };
                }

                var found = _processTree.FindByImage(plan.Prefix, plan.GameImage);
                if (found is not null)
                {
                    gamePid = found.Pid;
                    plan.GamePid = found.Pid;
                    MarkPhase("AwaitImage", phaseSw.ElapsedMilliseconds, $"pid={found.Pid}");
                    break;
                }

                if (DateTimeOffset.Now > imageDeadline)
                {
                    MarkPhase("AwaitImage", phaseSw.ElapsedMilliseconds, "超时");
                    await SafeCleanupAsync(plan, ct).ConfigureAwait(false);
                    MarkPhase("Cleanup", 0);
                    return new LaunchResult
                    {
                        Success = false,
                        Error = new LaunchError.GameExitedDuringStartup(-1, _paths.GameLogFile,
                            $"等待 {plan.WaitImageTimeout.TotalSeconds:0}s 仍未在 prefix 里找到 {plan.GameImage}。"),
                        Phases = phases,
                        StartedAt = startedAt,
                        ExitedAt = DateTimeOffset.Now,
                    };
                }

                await Task.Delay(200, ct).ConfigureAwait(false);
            }

            // ─────────────── AwaitReady ───────────────
            Report(Phase.AwaitReady, "等待就绪…", gamePid);
            phaseSw.Restart();
            var ready = await AwaitReadyAsync(plan, gamePid!.Value, imageEpoch, ct).ConfigureAwait(false);
            MarkPhase("AwaitReady", phaseSw.ElapsedMilliseconds, ready.ToString());

            if (gamePid is { } deadPid && _processTree.IsDeadOrZombie(deadPid))
            {
                MarkPhase("AwaitReady", phaseSw.ElapsedMilliseconds, "游戏在保护期内退出");
                await SafeCleanupAsync(plan, ct).ConfigureAwait(false);
                MarkPhase("Cleanup", 0);
                return new LaunchResult
                {
                    Success = false,
                    Error = new LaunchError.GameExitedDuringStartup(-1, _paths.GameLogFile,
                        "游戏在保护期内退出——多半是 prefix 不是冷的，或启动路径经过了 CreateProcessW。"),
                    Phases = phases,
                    StartedAt = startedAt,
                    ExitedAt = DateTimeOffset.Now,
                };
            }

            signal = ready;

            // ─────────────── PostLaunch ───────────────
            Report(Phase.PostLaunch, "启动辅助…", gamePid, signal);
            phaseSw.Restart();
            var running = new List<ExternalProgramHandle>();
            foreach (var program in plan.ExternalPrograms.Where(p => p.Trigger == Trigger.Auto))
            {
                // 硬约束：只可能在 AwaitReady 成功之后走到这里
                var handle = StartExternalProgram(program, plan, runningRecords: externalRecords);
                if (handle is not null)
                {
                    running.Add(handle);
                }
            }

            // AfterGameReady 时机的文件写入
            foreach (var program in plan.ExternalPrograms)
            {
                foreach (var write in program.PreWrites.Where(w => w.When == FileWriteWhen.AfterGameReady))
                {
                    WriteFileSpec(write, plan.Keywords);
                }
            }

            MarkPhase("PostLaunch", phaseSw.ElapsedMilliseconds, $"{running.Count} 个自动程序");

            // ─────────────── Running ───────────────
            Report(Phase.Running, "运行中", gamePid, signal);
            phaseSw.Restart();
            var exitEpoch = DateTimeOffset.Now;
            var gameExitCode = -1;
            try
            {
                await child.WaitForExitAsync(ct).ConfigureAwait(false);
                gameExitCode = child.ExitCode;
            }
            catch (OperationCanceledException)
            {
                // 取消只停止等待，不杀游戏
            }
            catch (InvalidOperationException)
            {
                // 进程已回收
            }

            // 收敛：等所有 wine 客户端退出（最多 30s）
            var settleSw = Stopwatch.StartNew();
            while (settleSw.Elapsed < plan.RunningSettle)
            {
                if (_processTree.GetPrefixClients(plan.Prefix).Count == 0)
                {
                    break;
                }

                await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
            }

            MarkPhase("Running", phaseSw.ElapsedMilliseconds, $"exit={gameExitCode}");

            // ─────────────── Cleanup ───────────────
            Report(Phase.Cleanup, "收尾中…", gamePid, signal);
            phaseSw.Restart();
            foreach (var handle in running)
            {
                await handle.ApplyExitPolicyAsync(TimeSpan.FromSeconds(3), CancellationToken.None).ConfigureAwait(false);
                externalRecords.Add(new ExternalProgramRecord(
                    handle.Id, handle.Plan.Trigger, "AwaitReady", handle.Pid, handle.ExitCode, null));
                handle.Dispose();
            }

            MarkPhase("Cleanup", phaseSw.ElapsedMilliseconds);

            return new LaunchResult
            {
                Success = true,
                Phases = phases,
                ExternalPrograms = externalRecords,
                GamePid = gamePid,
                Signal = signal,
                StartedAt = startedAt,
                ExitedAt = DateTimeOffset.Now,
                GameExitCode = gameExitCode,
            };
        }
    }


    /// <summary>
    /// 手动拉起一个 <see cref="Trigger.Manual"/> 外部程序（游戏 Running 之后才允许）。
    /// </summary>
    public ExternalProgramHandle? StartManual(ExternalProgramPlan program, LaunchPlan plan, out string? error)
    {
        error = null;
        if (program.Trigger != Trigger.Manual)
        {
            error = "该程序不是手动触发";
            return null;
        }

        var records = new List<ExternalProgramRecord>();
        var handle = StartExternalProgram(program, plan, records);
        error = records.FirstOrDefault(r => r.Error is not null)?.Error;
        return handle;
    }


    private ExternalProgramHandle? StartExternalProgram(
        ExternalProgramPlan program,
        LaunchPlan plan,
        List<ExternalProgramRecord> runningRecords)
    {
        try
        {
            var executable = program.Executable;
            var arguments = program.Arguments;

            if (program.Kind == ProgramKind.WindowsExe)
            {
                // 走 wine 拉起：包一层 wine 二进制
                var wineArgs = new List<string> { executable };
                wineArgs.AddRange(arguments);
                executable = plan.WineBinary;
                arguments = wineArgs;
            }

            if (!File.Exists(program.Executable) && program.Kind == ProgramKind.WindowsExe)
            {
                runningRecords.Add(new ExternalProgramRecord(
                    program.Id, program.Trigger, "AwaitReady", null, null,
                    $"文件不存在：{program.Executable}"));
                return null;
            }

            var request = new ProcessRequest
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = program.WorkingDirectory,
                Environment = program.Env,
                RedirectOutput = false,
                RedirectError = false,
                NewSession = true,
            };

            var process = _launcher.Start(request);
            return new ExternalProgramHandle(program, process, _controller);
        }
        catch (Exception ex)
        {
            runningRecords.Add(new ExternalProgramRecord(
                program.Id, program.Trigger, "AwaitReady", null, null, ex.Message));
            return null;
        }
    }


    private async Task<ReadySignal> AwaitReadyAsync(LaunchPlan plan, int gamePid, DateTimeOffset launchEpoch, CancellationToken ct)
    {
        var deadline = launchEpoch + plan.ReadyGrace;
        var playerLogs = FindPlayerLogs(plan);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (_processTree.IsDeadOrZombie(gamePid))
            {
                return ReadySignal.Grace;
            }

            // ① winedevice.exe —— 最强信号：保护驱动宿主已启动，ZwLoadDriver 成功
            if (HasWineDevice(plan.Prefix))
            {
                return ReadySignal.WineDevice;
            }

            // ② Player.log 的 mtime >= launchEpoch —— Unity 已开始初始化
            foreach (var log in playerLogs)
            {
                try
                {
                    if (File.Exists(log) && File.GetLastWriteTimeUtc(log) >= launchEpoch.UtcDateTime)
                    {
                        return ReadySignal.PlayerLog;
                    }
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            // ③ grace 超时兜底
            if (DateTimeOffset.Now >= deadline)
            {
                return ReadySignal.Grace;
            }

            await Task.Delay(100, ct).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// 本 prefix 里是否已出现 <c>winedevice.exe</c>。
    /// </summary>
    private bool HasWineDevice(string prefix)
    {
        // winedevice.exe 一定是本 prefix 的客户端，所以沿 prefix 客户端集合判定，
        // 避免误判其他 prefix（或多开的 another launcher）里的同名进程。
        foreach (var device in _processTree.FindByName("winedevice.exe"))
        {
            if (_processTree.GetPrefixClients(prefix).Any(c => c.Pid == device.Pid))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// 找 <c>Player.log</c>。实际路径是
    /// <c>&lt;prefix&gt;/drive_c/users/*/AppData/LocalLow/miHoYo/&lt;game&gt;/Player.log</c>，
    /// 用 glob 匹配 <c>*/Player.log</c> 更稳（大小写与笔误都能兜住）。
    /// </summary>
    public static IReadOnlyList<string> FindPlayerLogs(LaunchPlan plan)
    {
        var result = new List<string>();
        var users = Path.Join(plan.Prefix, "drive_c", "users");
        if (!Directory.Exists(users))
        {
            return result;
        }

        try
        {
            foreach (var user in Directory.EnumerateDirectories(users))
            {
                var localLow = Path.Join(user, "AppData", "LocalLow");
                if (!Directory.Exists(localLow))
                {
                    continue;
                }

                foreach (var vendor in Directory.EnumerateDirectories(localLow))
                {
                    foreach (var game in Directory.EnumerateDirectories(vendor))
                    {
                        var log = Path.Join(game, "Player.log");
                        if (File.Exists(log))
                        {
                            result.Add(log);
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // ignore
        }

        return result;
    }


    private async Task SafeCleanupAsync(LaunchPlan plan, CancellationToken ct)
    {
        try
        {
            await _prefixManager.CleanAsync(plan.Prefix, plan.WineBinary, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // ignore
        }
    }


    private void WriteFileSpec(FileWriteSpec spec, IReadOnlyDictionary<string, string?> keywords)
    {
        try
        {
            var path = KeywordReplacer.Replace(spec.Path, keywords);
            var content = KeywordReplacer.Replace(spec.Content, keywords);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            AtomicFile.WriteAllText(path, content);
        }
        catch (Exception)
        {
            // 写文件失败不终止启动
        }
    }


    /// <summary>
    /// 把子进程 stdout/stderr 泵到 <c>game.log</c>，并限制文件大小。
    /// </summary>
    private void AttachLogPump(Process process, string logPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logPath))!);
            var fs = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            var writer = new StreamWriter(fs) { AutoFlush = true };

            void Pump(StreamReader reader)
            {
                try
                {
                    string? line;
                    while ((line = reader.ReadLine()) is not null)
                    {
                        if (fs.Length > GameLogLimitBytes)
                        {
                            break;
                        }

                        lock (fs)
                        {
                            writer.WriteLine(line);
                        }
                    }
                }
                catch (Exception)
                {
                    // 进程退出后读流会抛异常，正常
                }
            }

            if (process.StartInfo.RedirectStandardOutput)
            {
                var t1 = new Thread(() => Pump(process.StandardOutput)) { IsBackground = true };
                t1.Start();
            }

            if (process.StartInfo.RedirectStandardError)
            {
                var t2 = new Thread(() => Pump(process.StandardError)) { IsBackground = true };
                t2.Start();
            }
        }
        catch (Exception)
        {
            // 日志泵失败不影响启动
        }
    }


    private static string Quote(string arg)
        => arg.Contains(' ') ? $"\"{arg}\"" : arg;
}
