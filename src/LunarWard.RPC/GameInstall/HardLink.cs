using System;
using System.IO;

namespace LunarWard.RPC.GameInstall;

/// <summary>
/// 平台替换：Win32 <c>CreateHardLink</c> → POSIX <c>link(2)</c>。
/// .NET 10 没有 <see cref="File"/> 硬链接 API，因此通过 libc 的 link 调用。
/// </summary>
internal static class HardLink
{

    public static bool TryCreate(string existingPath, string newPath)
    {
        try
        {
            if (File.Exists(newPath))
            {
                return false;
            }
            return NativeMethods.Link(existingPath, newPath) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

}
