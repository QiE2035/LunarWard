// 只发布 Linux 版本：整个程序集声明为 linux-only，Unix-only API 不再报 CA1416。
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("linux")]
