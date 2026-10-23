using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using LunarWard.Launch;
using LunarWard.Platform;
using LunarWard.Wine;

namespace LunarWard.App.Services;

/// <summary>
/// <see cref="LaunchPhaseState"/> 与 <see cref="Phase"/> 的映射。
/// </summary>
public enum LaunchPhaseState
{
    Idle,
    Prepare,
    Launch,
    AwaitImage,
    AwaitReady,
    PostLaunch,
    Running,
    Cleanup,
    Error,
}


/// <summary>
/// 一次"当前保存的游戏档案"的运行时门面：把 <see cref="ProfileConfig"/> 变成
/// 计划、执行启动、复用同一份 <see cref="LaunchPlan"/> 给 shell。
/// </summary>
public sealed class LauncherService : IDisposable
{
    private readonly AppServices _services;

    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _cts;

    private LaunchEngine? _engine;

    private LaunchPlan? _currentPlan;

    private ExternalProgramHandle? _manualHandle;


    public LauncherService(AppServices services)
    {
        _services = services;
    }


    /// <summary>当前状态。</summary>
    public LaunchPhaseState State { get; private set; } = LaunchPhaseState.Idle;

    /// <summary>当前计划的游戏 pid（Running 之后）。</summary>
    public int? GamePid { get; private set; }

    public LaunchResult? LastResult { get; private set; }

    /// <summary>最近一次构建的 <see cref="LaunchPlan"/>；环境 shell 复用它。</summary>
    public LaunchPlan? CurrentPlan => _currentPlan;

    /// <summary>外部程序运行句柄（Id → handle）。</summary>
    public IReadOnlyDictionary<string, ExternalProgramHandle> RunningPrograms => _running;

    private readonly Dictionary<string, ExternalProgramHandle> _running = new(StringComparer.Ordinal);


    public event EventHandler<LaunchPhaseState>? StateChanged;

    public event EventHandler<LaunchResult>? Completed;


    private void SetState(LaunchPhaseState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }


    /// <summary>
    /// 构建启动计划（不启动）。UI 的"诊断"与环境 shell 都调它。
    /// </summary>
    public LaunchPlan BuildPlan(string profileId, ProfileConfig profile, bool dryRun = false)
    {
        var info = BuildGameInfo(profile);
        var factory = _services.CreatePlanFactory();
        var plan = factory.Build(profileId, profile, info, _services.Config.Launcher.DefaultRuntime, dryRun);
        _currentPlan = plan;
        return plan;
    }


    /// <summary>
    /// 把 <see cref="ProfileConfig"/> 映射成 <see cref="GameLaunchInfo"/>
    /// （层 8 的游戏特化由 <c>LunarWard.Games</c> 的 <c>GameAdapter</c> 提供）。
    /// </summary>
    public static GameLaunchInfo BuildGameInfo(ProfileConfig profile)
    {
        var edition = string.Equals(profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
            ? Games.GameEdition.Global
            : Games.GameEdition.China;

        var definition = Games.GameCatalog.Get(profile.Game, edition)
                         ?? Games.GameCatalog.GetChina(profile.Game);

        if (definition is null)
        {
            return GameLaunchInfo.Fallback(profile.Game, profile.Edition);
        }

        var adapter = Games.GameAdapter.BuildEnv(
            definition,
            wineWayland: profile.Wine.WineWayland,
            timeoutFix: profile.Wine.TimeoutFix,
            borderless: profile.Wine.Borderless,
            dx12: profile.Enhancements.Dx12,
            fsr: profile.Enhancements.Fsr);

        var arguments = Games.GameAdapter.BuildArguments(
            definition.Kind,
            borderless: profile.Wine.Borderless,
            dx12: profile.Enhancements.Dx12,
            fsr: profile.Enhancements.Fsr);

        return new GameLaunchInfo
        {
            Kind = definition.Kind,
            Executable = definition.Executable,
            DataDirectory = definition.DataDirectory,
            GameBiz = definition.GameBiz,
            DefaultArguments = definition.DefaultArguments,
            GameEnv = adapter.Env,
            ExtraArguments = arguments,
        };
    }


    /// <summary>
    /// 启动游戏（异步，七相位全流程）。
    /// </summary>
    public async Task<LaunchResult> StartAsync(string profileId, ProfileConfig profile)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (State is not (LaunchPhaseState.Idle or LaunchPhaseState.Error))
            {
                return LastResult ?? LaunchResult.Fail(new LaunchError.Preflight("已有启动任务在进行"));
            }

            SetState(LaunchPhaseState.Prepare);
            _cts = new CancellationTokenSource();

            LaunchPlan plan;
            try
            {
                plan = BuildPlan(profileId, profile);
            }
            catch (RuntimeNotFoundException ex)
            {
                return Fail(new LaunchError.RuntimeNotFound(ex.RuntimeName, ex.Detail));
            }

            // B 站三条件（硬拒绝，见 docs/04-FEATURES.md §2.5）
            var channelError = ValidateChannel(profile, plan);
            if (channelError is not null)
            {
                return Fail(channelError);
            }

            _engine = new LaunchEngine(
                _services.Processes, _services.Controller, _services.Launcher, _services.PrefixManager,
                _services.Paths, _services.Dxvk);

            _engine.PhaseChanged += Engine_PhaseChanged;

            LaunchResult result;
            try
            {
                result = await _engine.RunAsync(plan, _cts.Token).ConfigureAwait(false);
            }
            finally
            {
                _engine.PhaseChanged -= Engine_PhaseChanged;
            }

            GamePid = result.GamePid;

            var report = LaunchReport.From(plan, result);
            try
            {
                report.WriteTo(_services.Paths.LaunchReportFile);
            }
            catch (Exception)
            {
                // 报告写失败不影响结果
            }

            LastResult = result;

            // 时长统计：复用相位引擎的起止时间（docs/05-ROADMAP.md §3.3）
            if (result.Success)
            {
                _services.PlayTime.Record(profileId, result);
            }

            SetState(result.Success ? LaunchPhaseState.Idle : LaunchPhaseState.Error);
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            var result = new LaunchResult { Success = false, StartedAt = DateTimeOffset.Now, ExitedAt = DateTimeOffset.Now };
            SetState(LaunchPhaseState.Idle);
            return result;
        }
        catch (Exception ex)
        {
            return Fail(new LaunchError.Preflight(ex.Message));
        }
        finally
        {
            _gate.Release();
        }

        LaunchResult Fail(LaunchError error)
        {
            var result = LaunchResult.Fail(error);
            LastResult = result;
            SetState(LaunchPhaseState.Error);
            Completed?.Invoke(this, result);
            return result;
        }
    }


    private LaunchError? ValidateChannel(ProfileConfig profile, LaunchPlan plan)
    {
        if (!string.Equals(profile.Channel, "Bilibili", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var definition = Games.GameCatalog.Detect(plan.GameDirectory);
        if (definition is null)
        {
            // 目录里没有可识别的 exe：不做强判断，交给运行时报错
            return null;
        }

        var check = Games.ChannelValidator.Validate(Games.GameChannel.Bilibili, definition, plan.GameDirectory);
        if (!check.Ok)
        {
            return new LaunchError.ChannelPrerequisiteMissing(check.Error ?? "B 站渠道前置条件不满足");
        }

        if (check.NeedsConfigRewrite)
        {
            try
            {
                Games.ChannelValidator.ApplyChannel(definition, plan.GameDirectory, Games.GameChannel.Bilibili);
            }
            catch (Exception ex)
            {
                return new LaunchError.ChannelPrerequisiteMissing($"写 config.ini 失败：{ex.Message}");
            }
        }

        return null;
    }


    private void Engine_PhaseChanged(object? sender, PhaseProgress e)
    {
        var state = e.Phase switch
        {
            Phase.Prepare => LaunchPhaseState.Prepare,
            Phase.Launch => LaunchPhaseState.Launch,
            Phase.AwaitImage => LaunchPhaseState.AwaitImage,
            Phase.AwaitReady => LaunchPhaseState.AwaitReady,
            Phase.PostLaunch => LaunchPhaseState.PostLaunch,
            Phase.Running => LaunchPhaseState.Running,
            Phase.Cleanup => LaunchPhaseState.Cleanup,
            _ => LaunchPhaseState.Idle,
        };
        if (e.GamePid is { } pid)
        {
            GamePid = pid;
        }

        SetState(state);
    }


    /// <summary>
    /// 手动拉起一个 <see cref="Trigger.Manual"/> 外部程序（仅游戏 Running 时允许）。
    /// </summary>
    public string? StartManualProgram(string programId)
    {
        if (_currentPlan is null)
        {
            return "还没有启动过游戏";
        }

        if (State != LaunchPhaseState.Running)
        {
            return "需先启动游戏";
        }

        var plan = _currentPlan.ExternalPrograms.FirstOrDefault(p => p.Id == programId);
        if (plan is null)
        {
            return "找不到该程序";
        }

        _engine ??= new LaunchEngine(
            _services.Processes, _services.Controller, _services.Launcher, _services.PrefixManager,
            _services.Paths, _services.Dxvk);

        var handle = _engine.StartManual(plan, _currentPlan, out var error);
        if (handle is null)
        {
            return error ?? "启动失败";
        }

        _running[programId] = handle;
        _manualHandle = handle;
        return null;
    }


    /// <summary>请求停止（只取消等待，不主动杀游戏——由用户决定）。</summary>
    public void RequestStop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }
    }


    public void Shutdown()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (Exception)
        {
            // ignore
        }

        foreach (var handle in _running.Values)
        {
            handle.Dispose();
        }

        _running.Clear();
    }


    public void Dispose()
    {
        Shutdown();
        _gate.Dispose();
        _cts?.Dispose();
    }
}


/// <summary>
/// 启动按钮状态机（文案直接映射 <see cref="Phase"/>）。
/// </summary>
/// <remarks>见 <c>docs/04-FEATURES.md</c> §5.2。</remarks>
public static class LaunchButtonState
{
    public static (string Text, bool Enabled) For(LaunchPhaseState state) => state switch
    {
        LaunchPhaseState.Idle => ("启动", true),
        LaunchPhaseState.Prepare => ("准备中…", false),
        LaunchPhaseState.Launch => ("启动中…", false),
        LaunchPhaseState.AwaitImage => ("等待游戏…", false),
        LaunchPhaseState.AwaitReady => ("等待就绪…", false),
        LaunchPhaseState.PostLaunch => ("启动辅助…", false),
        LaunchPhaseState.Running => ("停止游戏", true),
        LaunchPhaseState.Cleanup => ("收尾中…", false),
        LaunchPhaseState.Error => ("重试", true),
        _ => ("启动", true),
    };
}
