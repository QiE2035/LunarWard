using System.Runtime.InteropServices;

namespace LunarWard.Platform.Unix;

/// <summary>
/// 本层唯一的 libc P/Invoke 集合。所有系统调用细节集中在这里，
/// 上层（<c>LunarWard.Wine</c> / <c>Launch</c> / <c>Games</c>）不允许再写 DllImport。
///
/// <para>只保留实际被调用的声明；PTY（<c>openpty</c>/<c>ptsname</c>/<c>ioctl</c>）、
/// <c>flock</c>、<c>waitpid</c> 等随终端控件换成 NuGet 包、文件锁实现删除后已无调用点，
/// 2026-10-25 审计批次 C 一并清掉，需要时按 git 历史恢复。</para>
/// </summary>
internal static partial class Libc
{
    public const int SIGTERM = 15;
    public const int SIGKILL = 9;
    public const int SIGHUP = 1;

    [LibraryImport("libc", EntryPoint = "getuid")]
    public static partial uint GetUid();

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    public static partial int Kill(int pid, int sig);

    /// <summary><c>link(2)</c>：创建硬链接。上游用 <c>Kernel32.CreateHardLink</c>，.NET 无对应 BCL API。</summary>
    [LibraryImport("libc", EntryPoint = "link", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int Link(string oldPath, string newPath);
}
