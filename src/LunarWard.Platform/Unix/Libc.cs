using System.Runtime.InteropServices;

namespace LunarWard.Platform.Unix;

/// <summary>
/// 本层唯一的 libc P/Invoke 集合。所有系统调用细节集中在这里，
/// 上层（<c>LunarWard.Wine</c> / <c>Launch</c> / <c>Games</c>）不允许再写 DllImport。
/// </summary>
internal static partial class Libc
{
    /// <summary>可执行文件路径长度上限（Linux 上为 PATH_MAX）。</summary>
    public const int PATH_MAX = 4096;


    public const int LOCK_SH = 1;
    public const int LOCK_EX = 2;
    public const int LOCK_NB = 4;
    public const int LOCK_UN = 8;

    public const int SIGTERM = 15;
    public const int SIGKILL = 9;
    public const int SIGHUP = 1;
    public const int SIGCONT = 18;


    [LibraryImport("libc", EntryPoint = "flock", SetLastError = true)]
    public static partial int Flock(int fd, int operation);


    [LibraryImport("libc", EntryPoint = "getuid")]
    public static partial uint GetUid();


    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    public static partial int Kill(int pid, int sig);


    [LibraryImport("libc", EntryPoint = "waitpid", SetLastError = true)]
    public static partial int WaitPid(int pid, out int status, int options);


    [LibraryImport("libc", EntryPoint = "openpty", SetLastError = true)]
    public static partial int OpenPty(out int master, out int slave, IntPtr name, IntPtr termp, IntPtr winp);


    [LibraryImport("libc", EntryPoint = "grantpt", SetLastError = true)]
    public static partial int GrantPt(int fd);


    [LibraryImport("libc", EntryPoint = "unlockpt", SetLastError = true)]
    public static partial int UnlockPt(int fd);


    [LibraryImport("libc", EntryPoint = "ptsname", SetLastError = true)]
    public static partial IntPtr PtsName(int fd);


    [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    public static partial int Ioctl(int fd, nuint request, ref ulong arg);


    [LibraryImport("libc", EntryPoint = "setsid", SetLastError = true)]
    public static partial int SetSid();


    [LibraryImport("libc", EntryPoint = "dup2", SetLastError = true)]
    public static partial int Dup2(int oldfd, int newfd);


    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    public static partial int Close(int fd);


    [LibraryImport("libc", EntryPoint = "read", SetLastError = true)]
    public static partial nint Read(int fd, IntPtr buffer, nint count);


    [LibraryImport("libc", EntryPoint = "write", SetLastError = true)]
    public static partial nint Write(int fd, IntPtr buffer, nint count);


    [DllImport("libc", EntryPoint = "execvp", SetLastError = true)]
    public static extern int ExecVp(string file, IntPtr[] argv);


    // TIOCSWINSZ：窗口大小变更，用于 SIGWINCH
    public static nuint TIOCSWINSZ => OperatingSystem.IsLinux() ? 0x5414u : 0;
}
