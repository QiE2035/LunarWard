namespace LunarWard.Platform;

/// <summary>
/// XDG 自启动项（<c>$XDG_CONFIG_HOME/autostart/lunarward.desktop</c>）。
/// </summary>
/// <remarks>
/// Starward 在 Windows 用注册表 <c>HKCU\...\Run</c> 做开机自启；Linux 的等价物是
/// <a href="https://specifications.freedesktop.org/autostart-spec/latest/">XDG Autostart</a>
/// 目录里的 <c>.desktop</c> 文件。这里只负责文件本身，何时写/删由设置页决定。
/// </remarks>
public static class AutostartEntry
{
    /// <summary>自启文件名（与主程序同名，便于用户在文件管理器里认出来）。</summary>
    public const string FileName = "lunarward.desktop";


    /// <summary>默认的 <c>$XDG_CONFIG_HOME</c>（未设置时退回 <c>~/.config</c>）。</summary>
    public static string DefaultConfigHome()
    {
        string? configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return string.IsNullOrEmpty(configHome)
            ? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : configHome;
    }


    /// <summary>自启动项路径。</summary>
    public static string PathFor(string? configHome = null)
        => Path.Join(configHome ?? DefaultConfigHome(), "autostart", FileName);


    /// <summary>自启动项是否存在。</summary>
    public static bool IsEnabled(string? configHome = null) => File.Exists(PathFor(configHome));


    /// <summary>当前可执行文件路径（<c>./lunarward</c> 或 dotnet 宿主下的 apphost）。</summary>
    public static string CurrentExecutable()
        => Environment.ProcessPath
           ?? Path.Join(AppContext.BaseDirectory, "lunarward");


    /// <summary>
    /// 生成 <c>.desktop</c> 内容。<paramref name="execCommand"/> 按 Desktop Entry 规范做引号转义；
    /// 追加 <c>--start-hidden</c>，启动后留在托盘而不是弹窗口。
    /// </summary>
    public static string BuildContent(string execCommand, string? comment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(execCommand);
        return $"""
            [Desktop Entry]
            Type=Application
            Name=LunarWard
            Comment={comment ?? "Start LunarWard minimized to the system tray"}
            Exec={QuoteExec(execCommand)} --start-hidden
            Terminal=false
            X-GNOME-Autostart-enabled=true

            """;
    }


    /// <summary>启用/禁用自启动。启用时写入（必要时创建 <c>autostart</c> 目录），禁用时删除。</summary>
    /// <returns>操作后是否处于启用状态。</returns>
    public static bool SetEnabled(bool enabled, string execCommand, string? configHome = null)
    {
        string path = PathFor(configHome);
        if (!enabled)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return false;
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, BuildContent(execCommand));
        return true;
    }


    /// <summary>
    /// Desktop Entry 的 <c>Exec</c> 引号规则：含空格或保留字符时整体用双引号包住，
    /// 并对 <c>"</c> <c>`</c> <c>$</c> <c>\</c> 做反斜杠转义；<c>%</c> 是字段码，写成 <c>%%</c>。
    /// </summary>
    internal static string QuoteExec(string command)
    {
        string escaped = command.Replace("%", "%%").Replace("\\", "\\\\")
            .Replace("\"", "\\\"").Replace("`", "\\`").Replace("$", "\\$");
        return escaped.Contains(' ') ? $"\"{escaped}\"" : escaped;
    }
}
