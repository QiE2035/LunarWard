// 测试只在 Linux 上跑（CI 与本地都是 Linux），整体标注后可直接测试 linux-only API。
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("linux")]
