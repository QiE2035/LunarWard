using System.Diagnostics;

namespace LunarWard.Launch;

/// <summary>一条 URL 协议命令。</summary>
public sealed record UrlProtocolCommand(string Verb, IReadOnlyDictionary<string, string> Arguments)
{
    /// <summary>原始 URI。</summary>
    public Uri? Source { get; init; }


    public string? Get(string key) => Arguments.TryGetValue(key, out var value) ? value : null;
}


/// <summary>
/// <c>lunarward://</c> URL 协议的解析与执行。
/// </summary>
/// <remarks>
/// <para>
/// 见 <c>docs/05-ROADMAP.md</c> §3.3："URL Protocol —— 上游用注册表，Linux 用
/// <c>.desktop</c> + <c>MimeType=x-scheme-handler/lunarward</c>"。
/// <c>packaging/lunarward.desktop</c> 已经声明了这个 MimeType，本类负责
/// 把进程参数里的 URI 解析成命令。
/// </para>
/// <para>
/// 支持的 URI：
/// </para>
/// <list type="bullet">
/// <item><c>lunarward://launch?profile=genshin-cn</c> —— 启动指定档案</item>
/// <item><c>lunarward://shell?profile=genshin-cn</c> —— 打开该档案的环境 shell</item>
/// <item><c>lunarward://gacha?profile=genshin-cn</c> —— 打开抽卡记录</item>
/// <item><c>lunarward://open</c> —— 仅聚焦主窗口（默认）</item>
/// </list>
/// <para>
/// <b>安全约束</b>：只接受这三种 verb，profile 只作为<b>键</b>去配置里查——
/// 绝不把 URI 里的值当路径/命令执行。未知 verb 一律降级为 <c>open</c> 并记录原因。
/// </para>
/// </remarks>
public static class UrlProtocol
{
    public const string Scheme = "lunarward";


    /// <summary>导出成 <c>.desktop</c> 的 MimeType（应与打包文件一致）。</summary>
    public const string MimeType = "x-scheme-handler/lunarward";


    private static readonly HashSet<string> KnownVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "open", "launch", "shell", "gacha",
    };


    /// <summary>
    /// 尝试把命令行参数解析成命令。没有 <c>lunarward://</c> 参数时返回 <c>null</c>。
    /// </summary>
    public static UrlProtocolCommand? TryParse(IEnumerable<string> args, out string? error)
    {
        error = null;
        var uriText = args.FirstOrDefault(a =>
            a.StartsWith(Scheme + "://", StringComparison.OrdinalIgnoreCase));

        if (uriText is null)
        {
            return null;
        }

        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            error = $"无法解析 URI：{uriText}";
            return null;
        }

        // lunarward://launch?profile=x  → Host = "launch"
        var verb = uri.Host;
        if (string.IsNullOrEmpty(verb))
        {
            // lunarward://?profile=x 或 lunarward:///launch 的形式
            verb = uri.AbsolutePath.Trim('/');
        }

        if (string.IsNullOrEmpty(verb) || !KnownVerbs.Contains(verb))
        {
            error = $"未知命令 '{verb}'，按 open 处理";
            verb = "open";
        }

        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(uri.Query))
        {
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var index = pair.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(pair[..index]);
                var value = Uri.UnescapeDataString(pair[(index + 1)..]);
                arguments[key] = value;
            }
        }

        return new UrlProtocolCommand(verb.ToLowerInvariant(), arguments) { Source = uri };
    }


    /// <summary>
    /// 生成注册用的命令行（写进 <c>.desktop</c> 的 <c>Exec=</c>）。
    /// </summary>
    public static string BuildDesktopExec(string executablePath) => $"{executablePath} %U";


    /// <summary>
    /// 检查 .desktop 文件是否已正确注册（供诊断/自检用）。
    /// </summary>
    public static bool IsDesktopEntryValid(string desktopFileContent)
        => desktopFileContent.Contains($"MimeType={MimeType}", StringComparison.Ordinal)
           && desktopFileContent.Contains("%U", StringComparison.Ordinal);


    /// <summary>把命令转成人类可读描述（日志与 UI 提示用）。</summary>
    public static string Describe(UrlProtocolCommand command) => command.Verb switch
    {
        "launch" => $"启动档案 {command.Get("profile") ?? "(默认)"}",
        "shell" => $"打开档案 {command.Get("profile") ?? "(默认)"} 的环境 shell",
        "gacha" => $"打开档案 {command.Get("profile") ?? "(默认)"} 的抽卡记录",
        _ => "显示主窗口",
    };
}
