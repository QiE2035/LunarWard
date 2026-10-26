using System.Reflection;

namespace LunarWard.Services;

/// <summary>
/// 应用版本号的唯一来源。
///
/// <para>改动前有三份几乎相同的实现（关于页、UIGF 窗口的 <c>export_app_version</c>、
/// 命令行横幅），都读 <see cref="AssemblyInformationalVersionAttribute"/> 再去掉
/// SourceLink 追加的 <c>+sha</c> 后缀。这里合并成一份，行为不变。</para>
///
/// <para>注意：<c>UpdateService</c> 解析的是 **GitHub release tag**（可带 <c>v</c> 前缀、
/// 预发布后缀、<c>1.2</c> 两段式），与应用版本号语义不同，不在这里处理。</para>
/// </summary>
internal static class AppVersion
{
    private static readonly Lazy<string> DisplayValue = new(ReadDisplay, isThreadSafe: true);

    /// <summary>显示用版本号，例如 <c>1.0.0</c>；拿不到时退回 <c>0.0.0</c>。</summary>
    public static string Display => DisplayValue.Value;

    private static string ReadDisplay()
    {
        var assembly = typeof(AppVersion).Assembly;
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // 去掉 SourceLink 的后缀（1.0.0+abcdef）
            int plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
