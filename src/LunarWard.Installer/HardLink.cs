using System.IO;
using System.Runtime.InteropServices;

namespace LunarWard.Installer;

/// <summary>
/// <c>link(2)</c> 的最小封装。
///
/// <para>上游用 <c>Vanara.PInvoke.Kernel32.CreateHardLink</c>。.NET 10 的 BCL
/// 只有 <c>File.CreateSymbolicLink</c> / <c>File.ResolveLinkTarget</c>，
/// <b>没有</b>创建硬链接的 API，所以只能直接调 libc。</para>
///
/// <para>本项目约定"所有 P/Invoke 集中在 <c>LunarWard.Platform</c>"，
/// 但本任务只允许改 <c>src/LunarWard.Installer/</c>，且 <c>Platform.Unix.Libc</c>
/// 是 <c>internal</c>。这里放一份最小实现；后续若允许改 Platform，
/// 应上提为 <c>LunarWard.Platform.Unix.Libc.Link</c>。</para>
/// </summary>
public static class HardLink
{
    // glibc 的 link(2) 错误码，只用于日志归因，不影响成功/失败判定
    public const int EPERM = 1;
    public const int EACCES = 13;
    public const int EEXIST = 17;
    public const int EXDEV = 18;
    public const int EMLINK = 31;
    public const int EROFS = 30;
    public const int ENOSYS = 38;
    public const int EOPNOTSUPP = 95;


    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int SysLink([MarshalAs(UnmanagedType.LPUTF8Str)] string oldPath,
                                     [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath);


    /// <summary>
    /// 创建硬链接，语义对齐上游 <c>Kernel32.CreateHardLink</c>：
    /// 失败一律返回 <c>false</c> 而不抛异常（调用方会回退到复制），
    /// 错误码通过 <paramref name="errno"/> 返回供日志使用。
    /// </summary>
    public static bool TryCreate(string linkPath, string targetPath, out int errno)
    {
        errno = 0;
        try
        {
            if (!File.Exists(targetPath))
            {
                errno = -1;
                return false;
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(linkPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (SysLink(targetPath, linkPath) == 0)
            {
                return true;
            }

            errno = Marshal.GetLastPInvokeError();
            return false;
        }
        catch (Exception)
        {
            errno = -1;
            return false;
        }
    }


    /// <summary>创建硬链接，不关心错误码。</summary>
    public static bool TryCreate(string linkPath, string targetPath)
        => TryCreate(linkPath, targetPath, out _);


    /// <summary>把 errno 翻译成用于日志/错误消息的说明。</summary>
    public static string DescribeError(int errno) => errno switch
    {
        0 => "success",
        EPERM => "EPERM: operation not permitted (filesystem may not support hard links)",
        EACCES => "EACCES: permission denied (filesystem may not support hard links)",
        EEXIST => "EEXIST: link path already exists",
        EXDEV => "EXDEV: cross-device link (paths are on different filesystems)",
        EROFS => "EROFS: read-only filesystem",
        EMLINK => "EMLINK: too many links",
        ENOSYS => "ENOSYS: filesystem does not implement link(2)",
        EOPNOTSUPP => "EOPNOTSUPP: filesystem does not support hard links",
        -1 => "target file not found",
        _ => $"errno {errno}",
    };
}
