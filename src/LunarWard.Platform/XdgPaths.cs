namespace LunarWard.Platform;

/// <summary>
/// XDG 基础目录。所有路径解析集中在此，避免各处硬编码 <c>~/.local/share</c>。
/// </summary>
public interface IXdgPaths
{
    /// <summary>配置目录，默认 <c>~/.config/lunarward</c>。</summary>
    string ConfigHome { get; }

    /// <summary>数据目录，默认 <c>~/.local/share/lunarward</c>（prefix、runners、dxvks、components）。</summary>
    string DataHome { get; }

    /// <summary>缓存目录，默认 <c>~/.cache/lunarward</c>（背景、日志、缩略图）。</summary>
    string CacheHome { get; }

    /// <summary>配置文件绝对路径：<c>config.json</c>。</summary>
    string ConfigFile { get; }

    /// <summary>启动报告绝对路径：<c>launch-report.json</c>。</summary>
    string LaunchReportFile { get; }

    /// <summary>日志目录。</summary>
    string LogDirectory { get; }

    /// <summary>游戏 stdout/stderr 文件。</summary>
    string GameLogFile { get; }

    /// <summary>托管 wine runner 根目录。</summary>
    string RunnersDirectory { get; }

    /// <summary>托管 dxvk 根目录。</summary>
    string DxvkDirectory { get; }

    /// <summary>本启动器自管的 wine prefix 根目录。</summary>
    string PrefixesDirectory { get; }

    /// <summary>an-anime-team 组件索引目录。</summary>
    string ComponentsDirectory { get; }

    /// <summary>背景缓存目录。</summary>
    string BackgroundDirectory { get; }

    /// <summary>B 站插件缓存目录。</summary>
    string BilibiliDirectory { get; }
}


/// <summary>
/// 基于环境变量的 XDG 实现。遵循 XDG Base Directory Specification：
/// 变量为空或未设置时回退到规范默认值。
/// </summary>
public sealed class XdgPaths : IXdgPaths
{
    public const string AppName = "lunarward";


    public static XdgPaths Default { get; } = new();


    public XdgPaths(string? configHome = null, string? dataHome = null, string? cacheHome = null)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
        {
            home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
        }

        ConfigHome = AppendApp(configHome ?? NonEmpty(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")) ?? Path.Join(home, ".config"));
        DataHome = AppendApp(dataHome ?? NonEmpty(Environment.GetEnvironmentVariable("XDG_DATA_HOME")) ?? Path.Join(home, ".local", "share"));
        CacheHome = AppendApp(cacheHome ?? NonEmpty(Environment.GetEnvironmentVariable("XDG_CACHE_HOME")) ?? Path.Join(home, ".cache"));
    }


    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;


    private static string AppendApp(string baseDir) => Path.Join(baseDir, AppName);


    public string ConfigHome { get; }

    public string DataHome { get; }

    public string CacheHome { get; }

    public string ConfigFile => Path.Join(ConfigHome, "config.json");

    public string LaunchReportFile => Path.Join(DataHome, "launch-report.json");

    public string LogDirectory => Path.Join(CacheHome, "logs");

    public string GameLogFile => Path.Join(LogDirectory, "game.log");

    public string RunnersDirectory => Path.Join(DataHome, "runners");

    public string DxvkDirectory => Path.Join(DataHome, "dxvks");

    public string PrefixesDirectory => Path.Join(DataHome, "prefixes");

    public string ComponentsDirectory => Path.Join(DataHome, "components");

    public string BackgroundDirectory => Path.Join(CacheHome, "background");

    public string BilibiliDirectory => Path.Join(CacheHome, "bilibili");


    /// <summary>
    /// 创建运行所需的全套目录（幂等）。
    /// </summary>
    public void EnsureDirectories()
    {
        foreach (var dir in new[]
                 {
                     ConfigHome, DataHome, CacheHome, LogDirectory,
                     RunnersDirectory, DxvkDirectory, PrefixesDirectory,
                     ComponentsDirectory, BackgroundDirectory, BilibiliDirectory,
                 })
        {
            Directory.CreateDirectory(dir);
        }
    }
}
