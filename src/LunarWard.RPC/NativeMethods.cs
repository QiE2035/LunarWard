using System;
using System.Runtime.InteropServices;

namespace LunarWard.RPC;

internal static partial class NativeMethods
{

    [LibraryImport("libc", EntryPoint = "getuid")]
    internal static partial uint GetUid();

    [LibraryImport("libc", EntryPoint = "link", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Link(string oldpath, string newpath);

}
