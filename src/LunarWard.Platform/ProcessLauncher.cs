using System.Diagnostics;

namespace LunarWard.Platform;

/// <summary>一次外部进程启动的请求。</summary>
public sealed record ProcessRequest
{
    public required string FileName { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public string? WorkingDirectory { get; init; }

    /// <summary>完整环境变量（<b>不是</b>增量——调用方负责合并十层）。</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    public bool RedirectOutput { get; init; }

    public bool RedirectError { get; init; }

    /// <summary>
    /// 是否让子进程进入独立进程组（<c>setsid</c>）。游戏必须独立，
    /// 否则启动器退出会把 SIGHUP 传给游戏。
    /// </summary>
    public bool NewSession { get; init; } = true;
}


/// <summary>
/// Unix 侧进程启动。启动引擎的 <c>Launch</c> 相位与外部程序都走这里。
/// </summary>
public interface IProcessLauncher
{
    /// <summary>启动并返回 <see cref="Process"/>；失败抛 <see cref="System.ComponentModel.Win32Exception"/>。</summary>
    Process Start(ProcessRequest request);
}


/// <summary>
/// 基于 <see cref="Process"/> 的实现。
/// </summary>
/// <remarks>
/// .NET 的 <see cref="Process"/> 不暴露 <c>setpgid</c>，因此用 <c>setsid(1)</c>
/// 包装一层来获得独立会话（<c>setsid wine ...</c>）。若系统没有 <c>setsid</c>
/// （极少见，coreutils 自带），退回直接启动。
/// </remarks>
public sealed class UnixProcessLauncher : IProcessLauncher
{
    private static readonly Lazy<string?> SetsidPath = new(() => Which("setsid"), LazyThreadSafetyMode.ExecutionAndPublication);


    public static UnixProcessLauncher Default { get; } = new();


    public Process Start(ProcessRequest request)
    {
        var fileName = request.FileName;
        var args = new List<string>(request.Arguments);

        if (request.NewSession && SetsidPath.Value is { } setsid)
        {
            // setsid 是 executable + args，不改环境
            args.Insert(0, fileName);
            fileName = setsid;
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = request.RedirectOutput,
            RedirectStandardError = request.RedirectError,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrEmpty(request.WorkingDirectory))
        {
            psi.WorkingDirectory = request.WorkingDirectory;
        }

        if (request.Environment is not null)
        {
            psi.Environment.Clear();
            foreach (var (k, v) in request.Environment)
            {
                psi.Environment[k] = v;
            }
        }

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();
        return process;
    }


    /// <summary><c>which</c> 的等价实现（不依赖 PATH 大小写或外部命令）。</summary>
    public static string? Which(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "/usr/local/bin:/usr/bin:/bin";
        foreach (var dir in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Join(dir, executable);
            if (File.Exists(candidate) && (File.GetUnixFileMode(candidate) & UnixFileMode.UserExecute) != 0)
            {
                return candidate;
            }
        }

        return null;
    }
}
