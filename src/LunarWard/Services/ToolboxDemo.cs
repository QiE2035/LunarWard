using LunarWard.Services;

namespace LunarWard.Services;

/// <summary>
/// 工具箱各战绩页的「演示数据」开关（自检/截图用）。
///
/// <para>设置 <c>LUNARWARD_TOOLBOX_DEMO=1</c> 后，战绩类页面不联网、直接灌样例数据，
/// 便于离线截图。14 个页面以前各自写了一遍
/// <c>ToolboxDemo.Enabled</c>，
/// 这里收成一处，避免改开关名时漏改。</para>
/// </summary>
internal static class ToolboxDemo
{
    public const string EnvVar = "LUNARWARD_TOOLBOX_DEMO";

    /// <summary>是否启用演示数据。进程内只读一次（环境变量不期望在运行期变化）。</summary>
    public static bool Enabled { get; } = Environment.GetEnvironmentVariable(EnvVar) == "1";
}
