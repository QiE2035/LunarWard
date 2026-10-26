using System.Text;
using System.Text.RegularExpressions;
using LunarWard.Core;

namespace LunarWard.Wine;

/// <summary>
/// Linux 侧的 <see cref="IGameInstallPathProvider"/>：从 Wine prefix 的
/// <c>user.reg</c> 里读 <c>[Software\\miHoYo\\...]</c> 的 <c>InstallPath</c>。
/// </summary>
/// <remarks>
/// <para>
/// 上游 Starward 用 <c>Registry.GetValue(regKey, "InstallPath", null)</c>。
/// Linux 没有注册表，但 Wine 把它存在文本文件里：<c>&lt;prefix&gt;/user.reg</c>。
/// 本类解析那段文本，并把 Windows 路径（<c>C:\Program Files\...</c>）翻译成
/// Linux 路径（<c>&lt;prefix&gt;/drive_c/Program Files/...</c>），
/// 或者反过来用 <c>Z:</c> 映射到宿主根目录。
/// </para>
/// <para>
/// <b>只读</b>：绝不写 <c>user.reg</c>（账号切换等写操作另见后续里程碑）。
/// </para>
/// </remarks>
public sealed class WineRegistryInstallPathProvider : IGameInstallPathProvider
{
    private readonly string _prefix;

    private readonly string? _driveZRoot;


    /// <param name="prefix">Wine prefix 路径（含 <c>user.reg</c>）。</param>
    /// <param name="driveZRoot">
    /// <c>Z:</c> 映射到的宿主目录；<c>null</c> 表示用默认的 <c>/</c>。
    /// </param>
    public WineRegistryInstallPathProvider(string prefix, string? driveZRoot = null)
    {
        _prefix = prefix;
        _driveZRoot = driveZRoot;
    }


    public string Prefix => _prefix;


    /// <summary>按 GameBiz 查询：先查该 GameBiz 的注册表键。</summary>
    public string? GetInstallPath(GameBiz gameBiz)
    {
        var key = gameBiz.GetGameRegistryKey();
        var value = GetLauncherPath(key);
        return value;
    }


    /// <summary>
    /// 读 <c>InstallPath</c>。参数既可以是完整注册表键
    /// （<c>HKEY_CURRENT_USER\Software\miHoYo\原神</c>），也可以是 Wine 侧相对路径
    /// （<c>Software\miHoYo\原神</c>）。
    /// </summary>
    public string? GetLauncherPath(string registryKey)
    {
        var userReg = Path.Join(_prefix, "user.reg");
        if (!File.Exists(userReg))
        {
            return null;
        }

        string content;
        try
        {
            content = File.ReadAllText(userReg, Encoding.UTF8);
        }
        catch (IOException)
        {
            return null;
        }

        var wineKey = ToWineKey(registryKey);
        if (wineKey is null)
        {
            return null;
        }

        var windowsPath = ParseInstallPath(content, wineKey);
        return windowsPath is null ? null : TranslateToLinux(windowsPath);
    }


    /// <summary>
    /// 从 <c>user.reg</c> 文本里解析指定键的 <c>InstallPath</c>（返回原始 Windows 路径）。
    /// </summary>
    /// <remarks>
    /// <c>user.reg</c> 的段落形如 <c>[Software\\\\miHoYo\\\\原神] 1600000000</c>：
    /// 注册表键里的每个 <c>\</c> 在文件里被转义成 <c>\\</c>。
    /// 值形如 <c>"InstallPath"="C:\\Program Files\\..."</c>（同样转义）。
    /// </remarks>
    public string? ParseInstallPath(string userRegContent, string registryKey)
    {
        if (string.IsNullOrEmpty(userRegContent) || string.IsNullOrEmpty(registryKey))
        {
            return null;
        }

        // 段落头：把注册表键里的 \ 转成 \\，与文件里的转义一致
        var escaped = registryKey.Replace("\\", "\\\\", StringComparison.Ordinal);
        var header = "[" + escaped + "]";

        var start = userRegContent.IndexOf(header, StringComparison.Ordinal);
        if (start < 0)
        {
            // 有些版本把键名写成 \\\\（双写），再试一次
            header = "[" + registryKey.Replace("\\", "\\\\\\\\", StringComparison.Ordinal) + "]";
            start = userRegContent.IndexOf(header, StringComparison.Ordinal);
        }

        if (start < 0)
        {
            return null;
        }

        var end = userRegContent.IndexOf("\n[", start + header.Length, StringComparison.Ordinal);
        var block = end < 0 ? userRegContent[start..] : userRegContent[start..end];

        // 找 "InstallPath"="..."（注意不要匹配到 InstallPathXxx）
        var match = Regex.Match(block, "^\"InstallPath\"=\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Multiline);
        if (!match.Success)
        {
            return null;
        }

        return UnescapeRegistryString(match.Groups[1].Value);
    }


    /// <summary>把 <c>HKEY_CURRENT_USER\Software\miHoYo\原神</c> 归一成 Wine 侧键名。</summary>
    public static string? ToWineKey(string registryKey)
    {
        if (string.IsNullOrWhiteSpace(registryKey))
        {
            return null;
        }

        var key = registryKey.Trim();
        foreach (var hive in new[]
                 {
                     "HKEY_CURRENT_USER\\",
                     "HKEY_LOCAL_MACHINE\\",
                     "HKCU\\",
                     "HKLM\\",
                 })
        {
            if (key.StartsWith(hive, StringComparison.OrdinalIgnoreCase))
            {
                key = key[hive.Length..];
                break;
            }
        }

        return key;
    }


    /// <summary>
    /// 解析 + 翻译：从 <c>user.reg</c> 文本里取出该键的 InstallPath，并转成 Linux 路径。
    /// </summary>
    public string? ResolveFromContent(string userRegContent, string registryKey)
    {
        var windowsPath = ParseInstallPath(userRegContent, registryKey);
        return windowsPath is null ? null : TranslateToLinux(windowsPath);
    }


    /// <summary>
    /// 注册表字符串反转义：<c>\\</c> → <c>\</c>，<c>\"</c> → <c>"</c>。
    /// </summary>
    public static string UnescapeRegistryString(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                builder.Append(value[++i]);
            }
            else
            {
                builder.Append(value[i]);
            }
        }

        return builder.ToString();
    }


    /// <summary>
    /// Windows 路径 → Linux 路径。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><c>C:\Foo\Bar</c> → <c>&lt;prefix&gt;/drive_c/Foo/Bar</c></item>
    /// <item><c>D:\Foo</c> → <c>&lt;prefix&gt;/drive_d/Foo</c>（若存在）</item>
    /// <item><c>Z:\home\u\games</c> → <c>&lt;driveZRoot&gt;/home/u/games</c>（默认 <c>/</c>）</item>
    /// <item>已经是 Unix 路径 → 原样返回</item>
    /// </list>
    /// </remarks>
    public string TranslateToLinux(string windowsPath)
    {
        if (string.IsNullOrWhiteSpace(windowsPath))
        {
            return windowsPath;
        }

        var path = windowsPath.Trim();

        // 已是 Unix 路径
        if (path.StartsWith('/'))
        {
            return path;
        }

        if (path.Length < 2 || path[1] != ':')
        {
            // 相对路径：相对于 prefix 的 drive_c
            return Path.Join(_prefix, "drive_c", path.Replace('\\', '/'));
        }

        var drive = char.ToLowerInvariant(path[0]);
        var rest = path.Length > 2 ? path[3..] : "";
        var segments = rest.Split('\\', StringSplitOptions.RemoveEmptyEntries).ToArray();

        if (drive == 'z')
        {
            var root = _driveZRoot ?? "/";
            return Path.Join(root, string.Join('/', segments));
        }

        return Path.Join(_prefix, $"drive_{drive}", string.Join('/', segments));
    }
}
