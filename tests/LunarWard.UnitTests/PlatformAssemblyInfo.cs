// 本项目的测试只在 Linux 上跑（CI 与本地都是 Linux；Windows 分支由 OperatingSystem.* 守卫）。
// 显式标注后，`File.Get/SetUnixFileMode` 这类 Unix-only API 的 CA1416 不再误报，也就不必
// 在全局 NoWarn 里压 CA1416。
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("linux")]
