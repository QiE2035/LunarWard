using System.Globalization;

namespace LunarWard.Platform;

/// <summary>进程状态。僵尸进程仍持有 <c>/proc/&lt;pid&gt;/environ</c>，但已不占用 wineserver。</summary>
public enum ProcessState
{
    Running,
    Zombie,
    Dead,
}


/// <summary>一个 Linux 进程的最小描述。</summary>
public sealed record ProcessInfo(
    int Pid,
    string Comm,
    string? ImageName,
    ProcessState State,
    DateTime? StartTime)
{
    /// <summary><c>/proc/&lt;pid&gt;/environ</c> 里的 <c>WINEPREFIX</c> 值（若读到）。</summary>
    public string? WinePrefix { get; init; }


    public override string ToString() => $"pid={Pid} comm={Comm} image={ImageName ?? "-"} state={State}";
}


/// <summary>
/// 进程发现。这是整个启动引擎的"眼睛"——不变式（游戏必须是该 prefix 的第一个
/// wine 客户端）能否维持，全靠这里的判定是否准确。
/// </summary>
/// <remarks>
/// 实现必须可注入（测试用内存实现），见 <c>docs/03-LAUNCH-ENGINE.md</c> §4
/// 与 <c>docs/05-ROADMAP.md</c> U9。
/// </remarks>
public interface IProcessTree
{
    /// <summary>列出所有 <c>WINEPREFIX == prefix</c> 的进程（排除僵尸与自身）。</summary>
    IReadOnlyList<ProcessInfo> GetPrefixClients(string prefix);

    /// <summary>在上述进程中找已映射指定镜像的。</summary>
    ProcessInfo? FindByImage(string prefix, string imageName);

    /// <summary>按名字找进程（用于 <c>winedevice.exe</c> 检测）。</summary>
    IReadOnlyList<ProcessInfo> FindByName(string name);

    /// <summary>读取进程的启动时间，用于与 launch epoch 比较。</summary>
    DateTime? GetStartTime(int pid);

    /// <summary>进程是否仍然存活（非僵尸）。</summary>
    bool IsAlive(int pid);

    /// <summary>进程当前是否已死或已成为僵尸。</summary>
    bool IsDeadOrZombie(int pid);
}


/// <summary>
/// 基于 <c>/proc</c> 的真实实现。
/// </summary>
/// <remarks>
/// 判定"是本 prefix 的客户"：<c>/proc/&lt;pid&gt;/environ</c> 中任一条目精确等于
/// <c>WINEPREFIX=&lt;prefix&gt;</c>，且 state != Z，且 pid != 自身。
/// 判定"游戏已加载"：<c>/proc/&lt;pid&gt;/maps</c> 任一行包含镜像名。
/// 必须按 maps 而非 comm——runner 的 steam stub 会把 unix comm 固定成 <c>steam.exe</c>。
/// </remarks>
public sealed class LinuxProcessTree : IProcessTree
{
    private readonly string _procRoot;

    private readonly int _selfPidWidth;

    private static readonly Lazy<DateTime> BootTime = new(ReadBootTime, LazyThreadSafetyMode.ExecutionAndPublication);


    /// <param name="procRoot">默认为 <c>/proc</c>；测试可指向临时目录以模拟快照。</param>
    public LinuxProcessTree(string procRoot = "/proc")
    {
        _procRoot = procRoot;
        _selfPidWidth = Environment.ProcessId;
    }


    /// <summary>是否禁用"排除自身"（测试用合成快照时，pid 与真实进程无关）。</summary>
    public bool ExcludeSelf { get; init; } = true;


    /// <summary>当前用户 uid；不匹配的进程跳过（读不到 environ）。</summary>
    public uint Uid { get; init; } = Unix.Libc.GetUid();


    public IReadOnlyList<ProcessInfo> GetPrefixClients(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return [];
        }

        var result = new List<ProcessInfo>();
        var normalized = NormalizePath(prefix);

        foreach (var pid in EnumeratePids())
        {
            if (ExcludeSelf && pid == _selfPidWidth)
            {
                continue;
            }

            var state = ReadState(pid);
            if (state is ProcessState.Zombie or ProcessState.Dead)
            {
                continue;
            }

            var environ = ReadEnviron(pid);
            if (environ is null)
            {
                continue;
            }

            var winePrefix = environ.FirstOrDefault(e => e.StartsWith("WINEPREFIX=", StringComparison.Ordinal))?["WINEPREFIX=".Length..];
            if (string.IsNullOrEmpty(winePrefix))
            {
                continue;
            }

            // 精确匹配；同时容忍 symlink / 相对路径写法（仍然不是前缀匹配）
            if (!string.Equals(winePrefix, prefix, StringComparison.Ordinal)
                && !string.Equals(NormalizePath(winePrefix), normalized, StringComparison.Ordinal))
            {
                continue;
            }

            result.Add(new ProcessInfo(pid, ReadComm(pid), null, state, GetStartTime(pid))
            {
                WinePrefix = winePrefix,
            });
        }

        return result;
    }


    public ProcessInfo? FindByImage(string prefix, string imageName)
    {
        if (string.IsNullOrEmpty(imageName))
        {
            return null;
        }

        foreach (var client in GetPrefixClients(prefix))
        {
            var maps = ReadMaps(client.Pid);
            if (maps is null)
            {
                continue;
            }

            if (maps.Contains(imageName, StringComparison.OrdinalIgnoreCase))
            {
                return client with { ImageName = imageName };
            }
        }

        return null;
    }


    public IReadOnlyList<ProcessInfo> FindByName(string name)
    {
        var result = new List<ProcessInfo>();
        if (string.IsNullOrEmpty(name))
        {
            return result;
        }

        foreach (var pid in EnumeratePids())
        {
            if (ExcludeSelf && pid == _selfPidWidth)
            {
                continue;
            }

            var comm = ReadComm(pid);
            if (!string.Equals(comm, name, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(comm + ".exe", name, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(comm, name + ".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var state = ReadState(pid);
            if (state is ProcessState.Zombie or ProcessState.Dead)
            {
                continue;
            }

            result.Add(new ProcessInfo(pid, comm, null, state, GetStartTime(pid)));
        }

        return result;
    }


    public DateTime? GetStartTime(int pid)
    {
        var stat = ReadStat(pid);
        if (stat is null)
        {
            return null;
        }

        // field 22（1-based）：starttime，单位 clock tick（USER_HZ，通常 100）
        // field 1 = pid，field 2 = comm（可能含空格和括号），field 3 = state
        var rest = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // rest[0] = state, rest[1] = ppid ... starttime 是 rest[19]
        if (rest.Length > 19 && long.TryParse(rest[19], CultureInfo.InvariantCulture, out var ticks))
        {
            return BootTime.Value.AddSeconds(ticks / 100.0);
        }

        return null;
    }


    public bool IsAlive(int pid) => ReadState(pid) == ProcessState.Running;


    public bool IsDeadOrZombie(int pid)
    {
        var state = ReadState(pid);
        return state is ProcessState.Zombie or ProcessState.Dead;
    }


    private IEnumerable<int> EnumeratePids()
    {
        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(_procRoot);
        }
        catch (Exception)
        {
            yield break;
        }

        foreach (var dir in dirs)
        {
            var name = Path.GetFileName(dir);
            if (name.Length > 0 && name.All(char.IsAsciiDigit) && int.TryParse(name, out var pid))
            {
                yield return pid;
            }
        }
    }


    private string? ReadText(int pid, string file)
    {
        try
        {
            var path = Path.Join(_procRoot, pid.ToString(CultureInfo.InvariantCulture), file);
            return File.ReadAllText(path);
        }
        catch (Exception)
        {
            // 权限不足 / 进程已退出 / 内核配置差异 —— 一律静默跳过
            return null;
        }
    }


    private ProcessState ReadState(int pid)
    {
        var stat = ReadStat(pid);
        if (stat is null)
        {
            return ProcessState.Dead;
        }

        var idx = stat.LastIndexOf(')');
        if (idx < 0 || idx + 2 >= stat.Length)
        {
            return ProcessState.Dead;
        }

        return stat[idx + 2] switch
        {
            'Z' or 'X' or 'x' => ProcessState.Zombie,
            _ => ProcessState.Running,
        };
    }


    private string? ReadStat(int pid) => ReadText(pid, "stat");


    private string ReadComm(int pid)
    {
        var comm = ReadText(pid, "comm")?.TrimEnd('\n');
        if (!string.IsNullOrEmpty(comm))
        {
            return comm;
        }

        var stat = ReadStat(pid);
        if (stat is null)
        {
            return "";
        }

        var open = stat.IndexOf('(');
        var close = stat.LastIndexOf(')');
        return open >= 0 && close > open ? stat[(open + 1)..close] : "";
    }


    private IEnumerable<string>? ReadEnviron(int pid)
    {
        // 先看 uid：只有自己的进程能读 environ（root 除外）
        if (Uid != 0)
        {
            var status = ReadText(pid, "status");
            if (status is not null)
            {
                var uidLine = status.Split('\n').FirstOrDefault(l => l.StartsWith("Uid:", StringComparison.Ordinal));
                if (uidLine is not null)
                {
                    var parts = uidLine[4..].Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length > 0 && uint.TryParse(parts[0], out var owner) && owner != Uid)
                    {
                        return null;
                    }
                }
            }
        }

        try
        {
            var path = Path.Join(_procRoot, pid.ToString(CultureInfo.InvariantCulture), "environ");
            var bytes = File.ReadAllBytes(path);
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            return text.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Exception)
        {
            return null;
        }
    }


    private string? ReadMaps(int pid)
    {
        try
        {
            var path = Path.Join(_procRoot, pid.ToString(CultureInfo.InvariantCulture), "maps");
            return File.ReadAllText(path);
        }
        catch (Exception)
        {
            return null;
        }
    }


    private static string NormalizePath(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception)
        {
            return path;
        }
    }


    private static DateTime ReadBootTime()
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/stat"))
            {
                if (line.StartsWith("btime ", StringComparison.Ordinal)
                    && long.TryParse(line[6..].Trim(), CultureInfo.InvariantCulture, out var btime))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(btime).LocalDateTime;
                }
            }
        }
        catch (Exception)
        {
            // ignore
        }

        return DateTime.Now;
    }
}
