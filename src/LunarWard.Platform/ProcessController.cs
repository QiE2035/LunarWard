using System.Diagnostics;

namespace LunarWard.Platform;

/// <summary>
/// 进程控制：发信号、等待退出、终止整棵进程树。
/// </summary>
public interface IProcessController
{
    /// <summary>发送信号；进程不存在时返回 false。</summary>
    bool Signal(int pid, int signal);

    /// <summary>SIGTERM，等 <paramref name="grace"/>，仍在则 SIGKILL。</summary>
    Task TerminateAsync(int pid, TimeSpan grace, CancellationToken ct = default);

    /// <summary>对整个进程组发 SIGHUP（shell 关闭时回收子进程）。</summary>
    void Hangup(int pid);

    /// <summary>等待进程退出，返回是否在超时内退出。</summary>
    Task<bool> WaitForExitAsync(int pid, TimeSpan timeout, CancellationToken ct = default);
}


/// <summary>基于 libc <c>kill(2)</c> 的实现。</summary>
public sealed class LinuxProcessController : IProcessController
{
    public const int SIGTERM = Unix.Libc.SIGTERM;
    public const int SIGKILL = Unix.Libc.SIGKILL;
    public const int SIGHUP = Unix.Libc.SIGHUP;


    public static LinuxProcessController Default { get; } = new();


    /// <remarks>
    /// <paramref name="pid"/> 为负数时表示"整个进程组"（<c>kill(2)</c> 的语义），
    /// 这是回收 shell 进程树的关键，不能拒绝。
    /// </remarks>
    public bool Signal(int pid, int signal)
    {
        if (pid == 0)
        {
            return false;
        }

        return Unix.Libc.Kill(pid, signal) == 0;
    }


    public async Task TerminateAsync(int pid, TimeSpan grace, CancellationToken ct = default)
    {
        if (!Signal(pid, SIGTERM))
        {
            return;
        }

        if (await WaitForExitAsync(pid, grace, ct).ConfigureAwait(false))
        {
            return;
        }

        // winedevice.exe 会忽略 SIGTERM，必须升级到 SIGKILL
        Signal(pid, SIGKILL);
        await WaitForExitAsync(pid, TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
    }


    public void Hangup(int pid)
    {
        // 负 pid 表示进程组
        if (pid > 0)
        {
            Unix.Libc.Kill(-pid, SIGHUP);
        }
    }


    /// <summary>向整个进程组发 SIGTERM。</summary>
    public bool TerminateGroup(int processGroupId, int signal = SIGTERM)
        => processGroupId > 0 && Unix.Libc.Kill(-processGroupId, signal) == 0;


    public async Task<bool> WaitForExitAsync(int pid, TimeSpan timeout, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            if (!IsAlive(pid))
            {
                return true;
            }

            await Task.Delay(50, ct).ConfigureAwait(false);
        }

        return !IsAlive(pid);
    }


    private static bool IsAlive(int pid)
    {
        try
        {
            var statPath = $"/proc/{pid}/stat";
            if (!File.Exists(statPath))
            {
                return false;
            }

            var stat = File.ReadAllText(statPath);
            var idx = stat.LastIndexOf(')');
            return idx < 0 || idx + 2 >= stat.Length || stat[idx + 2] is not ('Z' or 'X' or 'x');
        }
        catch (Exception)
        {
            return false;
        }
    }
}
