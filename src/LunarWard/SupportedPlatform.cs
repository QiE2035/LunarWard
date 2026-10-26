// LunarWard 只发布 Linux 版本（Wine/Proton 是运行前提），整个程序集声明为 linux-only：
// `File.Get/SetUnixFileMode` 这类 Unix-only API 不再触发 CA1416，同时将来误用 Windows
// 专用 API 会立刻变成警告（而不是被全局 NoWarn 吞掉）。
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

// 折叠 LunarWard.Platform / LunarWard.Installer 后，两者与本程序集同一 assembly，
// 不再需要为 Installer 开 IVT（测试工程的可见性由 csproj 的 InternalsVisibleTo 提供）。

[assembly: SupportedOSPlatform("linux")]
