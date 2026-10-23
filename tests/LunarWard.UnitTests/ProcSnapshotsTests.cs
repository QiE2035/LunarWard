using LunarWard.Platform;
using Xunit;

namespace LunarWard.UnitTests;

/// <summary>
/// 把临时目录当 <c>/proc</c> 用，验证真实实现
/// <see cref="LinuxProcessTree"/> 的判定逻辑（不是 fake 的判定）。
/// </summary>
public class ProcSnapshotsTests
{
    /// <summary>构造 <c>/proc/&lt;pid&gt;/{stat,status,environ,maps,comm}</c>。</summary>
    private static void WriteProc(TempDirectory temp, int pid, string comm, char state, uint uid, string[] environ, string? maps = null)
    {
        var dir = temp.Combine("proc", pid.ToString());
        Directory.CreateDirectory(dir);
        // stat: pid (comm) state ppid ... starttime 在 field 22
        var fields = new List<string> { state.ToString(), "1" };
        // 补齐到 field 22：fields 从 field 3 开始是 index 0；starttime 是 index 19
        while (fields.Count < 19)
        {
            fields.Add("0");
        }

        fields.Add("12345"); // starttime（field 22）
        File.WriteAllText(Path.Join(dir, "stat"), $"{pid} ({comm}) {string.Join(' ', fields)}");
        File.WriteAllText(Path.Join(dir, "comm"), comm + "\n");
        File.WriteAllText(Path.Join(dir, "status"), $"Name:\t{comm}\nUid:\t{uid}\t{uid}\t{uid}\t{uid}\n");
        var envBytes = string.Concat(environ.Select(e => e + "\0"));
        File.WriteAllBytes(Path.Join(dir, "environ"), System.Text.Encoding.UTF8.GetBytes(envBytes));
        if (maps is not null)
        {
            File.WriteAllText(Path.Join(dir, "maps"), maps);
        }
    }


    [Fact]
    public void PrefixClients_MatchesExactWinePrefixOnly()
    {
        using var temp = new TempDirectory();
        var uid = (uint)Environment.ProcessId.GetHashCode(); // 任意值
        var prefix = "/tmp/pfx-a";

        WriteProc(temp, 1001, "wine", 'S', uid, ["WINEPREFIX=/tmp/pfx-a", "PATH=/usr/bin"]);
        WriteProc(temp, 1002, "wine", 'S', uid, ["WINEPREFIX=/tmp/pfx-ab"]);  // 前缀相同但不是同一个
        WriteProc(temp, 1003, "wine", 'S', uid, ["PATH=/usr/bin"]);            // 没有 WINEPREFIX
        WriteProc(temp, 1004, "wine", 'Z', uid, ["WINEPREFIX=/tmp/pfx-a"]);     // 僵尸

        var tree = new LinuxProcessTree(temp.Combine("proc")) { Uid = uid, ExcludeSelf = false };
        var clients = tree.GetPrefixClients(prefix);

        Assert.Single(clients);
        Assert.Equal(1001, clients[0].Pid);
    }


    [Fact]
    public void ExcludeSelf_SkipsOwnPid()
    {
        using var temp = new TempDirectory();
        var uid = (uint)Environment.ProcessId.GetHashCode();
        var self = Environment.ProcessId;
        WriteProc(temp, self, "dotnet", 'S', uid, ["WINEPREFIX=/tmp/pfx"]);

        var tree = new LinuxProcessTree(temp.Combine("proc")) { Uid = uid, ExcludeSelf = true };
        Assert.Empty(tree.GetPrefixClients("/tmp/pfx"));
    }


    [Fact]
    public void ExcludeOwnedByOtherUid()
    {
        using var temp = new TempDirectory();
        var mine = (uint)1234;
        WriteProc(temp, 2001, "wine", 'S', mine, ["WINEPREFIX=/tmp/pfx"]);
        WriteProc(temp, 2002, "wine", 'S', mine + 1, ["WINEPREFIX=/tmp/pfx"]);

        var tree = new LinuxProcessTree(temp.Combine("proc")) { Uid = mine, ExcludeSelf = false };
        var clients = tree.GetPrefixClients("/tmp/pfx");
        Assert.Single(clients);
        Assert.Equal(2001, clients[0].Pid);
    }


    [Fact]
    public void FindByImage_UsesMapsNotComm()
    {
        // 实证：runner 的 steam stub 会把 unix comm 固定成 steam.exe，
        // 真游戏是它 fork 的子进程 —— 只有镜像映射可靠。
        using var temp = new TempDirectory();
        var uid = (uint)7;
        WriteProc(temp, 3001, "steam.exe", 'S', uid, ["WINEPREFIX=/tmp/pfx"],
            "/home/u/.local/share/wine/steam.exe\n/home/u/games/ZenlessZoneZero.exe\n");
        WriteProc(temp, 3002, "YuanShen.exe", 'S', uid, ["WINEPREFIX=/tmp/pfx"],
            "/home/u/.local/share/wine/steam.exe\n");

        var tree = new LinuxProcessTree(temp.Combine("proc")) { Uid = uid, ExcludeSelf = false };

        Assert.Equal(3001, tree.FindByImage("/tmp/pfx", "ZenlessZoneZero.exe")?.Pid);
        // comm 名对了但 maps 里没有 → 必须找不到
        Assert.Null(tree.FindByImage("/tmp/pfx", "YuanShen.exe"));
    }


    [Fact]
    public void FindByImage_IgnoresOtherPrefixes()
    {
        using var temp = new TempDirectory();
        var uid = (uint)7;
        WriteProc(temp, 4001, "wine", 'Z', uid, ["WINEPREFIX=/tmp/pfx"], "/games/YuanShen.exe");
        WriteProc(temp, 4002, "wine", 'S', uid, ["WINEPREFIX=/tmp/other"], "/games/YuanShen.exe");

        var tree = new LinuxProcessTree(temp.Combine("proc")) { Uid = uid, ExcludeSelf = false };
        Assert.Null(tree.FindByImage("/tmp/pfx", "YuanShen.exe"));
    }


    [Fact]
    public void FindByName_MatchesWithAndWithoutExeSuffix()
    {
        using var temp = new TempDirectory();
        var uid = (uint)7;
        WriteProc(temp, 5001, "winedevice.exe", 'S', uid, ["WINEPREFIX=/tmp/pfx"]);
        WriteProc(temp, 5002, "winedevice", 'S', uid, ["WINEPREFIX=/tmp/pfx"]);

        var tree = new LinuxProcessTree(temp.Combine("proc")) { Uid = uid, ExcludeSelf = false };
        Assert.Equal(2, tree.FindByName("winedevice.exe").Count);
    }


    [Fact]
    public void GetStartTime_ParsesStatField22()
    {
        using var temp = new TempDirectory();
        var uid = (uint)7;
        WriteProc(temp, 6001, "wine", 'S', uid, ["WINEPREFIX=/tmp/pfx"]);

        var tree = new LinuxProcessTree(temp.Combine("proc")) { Uid = uid, ExcludeSelf = false };
        var start = tree.GetStartTime(6001);
        Assert.NotNull(start);
        Assert.InRange(start!.Value, DateTime.Now.AddYears(-2), DateTime.Now.AddMinutes(5));
    }


    [Fact]
    public void DeadPid_IsReportedAsDead()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Combine("proc"));
        var tree = new LinuxProcessTree(temp.Combine("proc")) { Uid = 7, ExcludeSelf = false };
        Assert.True(tree.IsDeadOrZombie(999999));
        Assert.False(tree.IsAlive(999999));
    }
}
