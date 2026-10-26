using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace LunarWard.Installer;

/// <summary>
/// 父进程退出监听。
///
/// <para>上游 <c>Starward.RPC.Env.LifecycleManager</c> 是 <c>Starward.RPC</c> 宿主进程
/// 里的静态类：RPC 进程知道自己的父进程（UI 进程）退出后要取消所有安装任务。
/// 本项目没有独立宿主进程，安装器是进程内库，因此这里把它内联成一个
/// 可选的、显式启用的监听器：调用方传入要盯的 pid（通常是 avalonia 应用的 pid，
/// 或 wine 进程的 pid），退出后触发回调。</para>
///
/// <para><b>不复用 <c>System.Diagnostics.Process.Exited</c></b>：它依赖 <c>waitpid</c>
/// 且只在 Process 对象自身 fork 的子进程上可靠。这里直接轮询
/// <c>/proc/&lt;pid&gt;</c>，与 <c>LunarWard.Platform.LinuxProcessTree</c> 的做法一致，
/// 也避免与 winedevice 之类的 double-fork 进程交互出问题。</para>
/// </summary>
public sealed class ParentProcessWatcher : IDisposable
{
    private readonly int _parentPid;
    private readonly IInstallerLogger _logger;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _sync = new();

    private Task? _loop;
    private bool _disposed;


    /// <summary>父进程消失时触发一次。</summary>
    public event EventHandler<int>? ParentExited;


    public ParentProcessWatcher(int parentPid, TimeSpan? pollInterval = null, IInstallerLogger? logger = null)
    {
        _parentPid = parentPid;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        _logger = logger ?? NullInstallerLogger.Instance;
    }


    public int ParentPid => _parentPid;


    /// <summary>开始监听（幂等）。<paramref name="parentPid"/> ≤ 0 时不做任何事。</summary>
    public void Start()
    {
        if (_parentPid <= 0 || _disposed)
        {
            return;
        }

        lock (_sync)
        {
            _loop ??= Task.Run(() => WatchAsync(_cts.Token));
        }
    }


    private async Task WatchAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Watching parent process {pid}", _parentPid);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
                if (!IsProcessAlive(_parentPid))
                {
                    _logger.LogInformation("Parent process {pid} exited, stop all game install tasks.", _parentPid);
                    ParentExited?.Invoke(this, _parentPid);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Watch parent process {pid}", _parentPid);
        }
    }


    /// <summary>
    /// 进程是否还活着。僵尸进程（<c>Z</c>）视为已退出：
    /// wine 客户端被 SIGKILL 后父进程往往来不及 reap，若不排除僵尸，
    /// 停下来监听就永远触发不了。
    /// </summary>
    public static bool IsProcessAlive(int pid)
    {
        if (pid <= 0)
        {
            return false;
        }

        try
        {
            var statPath = $"/proc/{pid}/stat";
            if (!File.Exists(statPath))
            {
                return false;
            }

            var stat = File.ReadAllText(statPath);
            // <pid> (<comm>) <state> ...；comm 可能含空格与括号，取最后一个 ')' 之后
            var close = stat.LastIndexOf(')');
            if (close < 0 || close + 2 >= stat.Length)
            {
                return true;
            }

            var state = stat[close + 2];
            return state is not ('Z' or 'X' or 'x');
        }
        catch (Exception)
        {
            return false;
        }
    }


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
            // ignore
        }

        _cts.Dispose();
    }
}


/// <summary>
/// 从游戏日志/配置文本里解析版本号的小工具（上游用 <c>[GeneratedRegex]</c> 内联）。
/// </summary>
public static partial class InstallerTextPatterns
{
    [GeneratedRegex(@"game_version=(.+)")]
    public static partial Regex GameVersion();

    [GeneratedRegex(@"sdk_version=(.+)")]
    public static partial Regex SdkVersion();

    [GeneratedRegex(@"wpf_version=(.+)")]
    public static partial Regex WpfVersion();


    /// <summary>把 <see cref="Regex"/> 的第一个分组解析为 <see cref="Version"/>，失败返回 <c>null</c>。</summary>
    public static Version? TryParseVersion(Regex regex, string text)
    {
        var match = regex.Match(text);
        if (!match.Success || match.Groups.Count < 2)
        {
            return null;
        }

        return Version.TryParse(match.Groups[1].Value.Trim(), out var version) ? version : null;
    }
}
