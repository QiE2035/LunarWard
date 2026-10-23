using System.Text;
using LunarWard.Platform;
using LunarWard.Wine;

namespace LunarWard.UnitTests;

/// <summary>
/// 用临时目录模拟 <c>/proc</c> 快照的 <see cref="IProcessTree"/> 实现。
/// </summary>
/// <remarks>U9 的实现提示：不要真的读 <c>/proc</c>。</remarks>
public sealed class FakeProcessTree : IProcessTree
{
    public sealed record Entry(int Pid, string Comm, string? WinePrefix, ProcessState State = ProcessState.Running, string? Maps = null, DateTime? Start = null);

    public List<Entry> Processes { get; } = [];


    public FakeProcessTree Add(int pid, string comm, string? winePrefix, ProcessState state = ProcessState.Running, string? maps = null)
    {
        Processes.Add(new Entry(pid, comm, winePrefix, state, maps));
        return this;
    }


    private static bool SamePrefix(string? a, string b) => string.Equals(a, b, StringComparison.Ordinal);


    public IReadOnlyList<ProcessInfo> GetPrefixClients(string prefix)
        => Processes
            .Where(p => p.State != ProcessState.Zombie && SamePrefix(p.WinePrefix, prefix))
            .Select(p => new ProcessInfo(p.Pid, p.Comm, null, p.State, p.Start) { WinePrefix = p.WinePrefix })
            .ToList();


    public ProcessInfo? FindByImage(string prefix, string imageName)
    {
        foreach (var p in Processes.Where(p => p.State != ProcessState.Zombie && SamePrefix(p.WinePrefix, prefix)))
        {
            if (p.Maps is not null && p.Maps.Contains(imageName, StringComparison.OrdinalIgnoreCase))
            {
                return new ProcessInfo(p.Pid, p.Comm, imageName, p.State, p.Start) { WinePrefix = p.WinePrefix };
            }
        }

        return null;
    }


    public IReadOnlyList<ProcessInfo> FindByName(string name)
        => Processes
            .Where(p => p.State != ProcessState.Zombie
                        && (string.Equals(p.Comm, name, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(p.Comm, name + ".exe", StringComparison.OrdinalIgnoreCase)))
            .Select(p => new ProcessInfo(p.Pid, p.Comm, null, p.State, p.Start) { WinePrefix = p.WinePrefix })
            .ToList();


    public DateTime? GetStartTime(int pid) => Processes.FirstOrDefault(p => p.Pid == pid)?.Start;

    public bool IsAlive(int pid) => Processes.Any(p => p.Pid == pid && p.State == ProcessState.Running);

    public bool IsDeadOrZombie(int pid) => !IsAlive(pid);
}


/// <summary>临时目录，<c>Dispose</c> 时递归删除。</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory(string prefix = "lunarward-test-")
    {
        Path = System.IO.Path.Join(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Path);
    }


    public string Path { get; }


    public string Combine(params string[] parts)
    {
        var result = Path;
        foreach (var part in parts)
        {
            result = System.IO.Path.Join(result, part);
        }

        return result;
    }


    /// <summary>创建一个假的 wine 二进制（可执行）。</summary>
    public string CreateFakeWine()
    {
        var bin = System.IO.Path.Join(Path, "wine");
        File.WriteAllText(bin, "#!/bin/sh\necho wine-10.0 (fake)\n");
        File.SetUnixFileMode(bin, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return bin;
    }


    /// <summary>创建最小的可"有效"prefix（<c>system.reg</c> + <c>user.reg</c>）。</summary>
    public string CreatePrefix(string name = "prefix")
    {
        var prefix = Combine(name);
        Directory.CreateDirectory(prefix);
        File.WriteAllText(System.IO.Path.Join(prefix, "system.reg"), "WINE REGISTRY Version 2\n");
        File.WriteAllText(System.IO.Path.Join(prefix, "user.reg"), "WINE REGISTRY Version 2\n");
        return prefix;
    }


    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception)
        {
            // best effort
        }
    }
}
