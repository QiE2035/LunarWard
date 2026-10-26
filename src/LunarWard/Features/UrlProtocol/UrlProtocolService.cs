using LunarWard.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LunarWard.Features.UrlProtocol;

/// <summary>
/// 上游 <c>Starward.Features.UrlProtocol.UrlProtocolService</c> 的移植。
/// </summary>
/// <remarks>
/// <para>
/// 上游通过注册表把 <c>Starward://</c> 注册成 URL 协议（<c>HKCU\Software\Classes\Starward</c>）。
/// Linux 上的等价物是 freedesktop 的 <c>x-scheme-handler</c>：写一个
/// <c>~/.local/share/applications/lunarward-url-protocol.desktop</c>（<c>MimeType=x-scheme-handler/lunarward;</c>），
/// 再用 <c>xdg-mime default</c> 把它设成默认处理程序。
/// </para>
/// <para>
/// 协议 URL 的处理复用本仓库已有的 <see cref="StartupCommands"/>（上游是
/// <c>App.xaml.cs</c> 里 <c>HandleUrlProtocolAsync</c> + 服务容器；本仓库的
/// <c>startgame</c>/<c>playtime</c> 子命令与上游语义一致，且不依赖 UI 线程）。
/// </para>
/// <para>平台替换点逐条登记 docs/09-LINUX-DIVERGENCES.md。</para>
/// </remarks>
internal class UrlProtocolService
{

    /// <summary>本程序注册的 URL scheme（上游 <c>Starward</c>）。</summary>
    public const string Scheme = "lunarward";


    private const string DesktopFileName = "lunarward-url-protocol.desktop";


    private const string MimeType = "x-scheme-handler/lunarward";



    /// <summary>
    /// 对应上游 <c>RegisterProtocol</c>：写入 .desktop 并设为默认协议处理程序。
    /// </summary>
    public static void RegisterProtocol()
    {
        UnregisterProtocol();
        string exe = Environment.ProcessPath ?? Path.Join(AppContext.BaseDirectory, "LunarWard");
        string applications = GetUserApplicationsDirectory();
        Directory.CreateDirectory(applications);

        // Exec 的 %u 是「单个 URL」，与上游 "%1" 等价；路径含空格时按 desktop entry 规范加引号。
        string exec = exe.Contains(' ') ? $"\"{exe}\" %u" : $"{exe} %u";
        string desktop = $"""
            [Desktop Entry]
            Type=Application
            Name=LunarWard URL Handler
            Comment=Handle lunarward:// URLs
            Exec={exec}
            Terminal=false
            NoDisplay=true
            MimeType={MimeType};
            Categories=Utility;
            """;
        File.WriteAllText(Path.Join(applications, DesktopFileName), desktop, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // update-desktop-database 让 MIME 索引立刻生效；失败不致命（xdg-mime 仍可用）。
        RunQuietly("update-desktop-database", applications);

        // 设为默认处理程序（等价于上游写 HKCU\Software\Classes\Starward\Shell\Open\Command）。
        RunQuietly("xdg-mime", $"default {DesktopFileName} {MimeType}");
    }



    /// <summary>
    /// 对应上游 <c>UnregisterProtocol</c>：删除 .desktop、清理 mimeapps.list 里的关联。
    /// </summary>
    public static void UnregisterProtocol()
    {
        try
        {
            string applications = GetUserApplicationsDirectory();
            string desktop = Path.Join(applications, DesktopFileName);
            if (File.Exists(desktop))
            {
                File.Delete(desktop);
            }
            RunQuietly("update-desktop-database", applications);
            RemoveMimeAssociation(Path.Join(GetUserConfigDirectory(), "mimeapps.list"));
            // 有些发行版把用户级 mimeapps.list 放在 $XDG_DATA_HOME/applications 下。
            string dataApplications = Path.Join(GetUserDataDirectory(), "applications");
            RemoveMimeAssociation(Path.Join(dataApplications, "mimeapps.list"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }



    /// <summary>
    /// 对应上游 <c>HandleUrlProtocolAsync</c>。
    /// </summary>
    /// <returns>
    /// <c>true</c>：URL 已处理（上层应退出程序）；<c>false</c>：不是本程序处理的 URL
    /// （例如 <c>lunarward://test</c>，上游会继续打开 TestUrlProtocolWindow）。
    /// </returns>
    public static async Task<bool> HandleUrlProtocolAsync(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out Uri? uri))
            {
                if (uri.Host is "test")
                {
                    return false;
                }
                // 上游此处检查 AppConfig.UserDataFolder 是否为空（便携版首次启动前可能没初始化）。
                // 本仓库的数据目录由 XdgPaths 计算，始终有值（并在 AppServices 构造时创建）。
                if (uri.Host is "startgame" or "playtime")
                {
                    return await DispatchAsync(url).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            // 上游 User32.MessageBox；Linux 上没有等价的同步消息框，错误写 stderr（由 .desktop 的
            // Terminal=false 捕获不到，用户可在应用日志里看到）。
            Console.Error.WriteLine($"[UrlProtocol] {url}: {ex}");
            return true;
        }
        return false;
    }



    /// <summary>
    /// 分发到 <see cref="StartupCommands"/>（上游是 <c>AppConfig.GetService&lt;GameLauncherService&gt;()</c>
    /// / <c>PlayTimeRecordService</c> 的直接调用）。
    /// </summary>
    private static async Task<bool> DispatchAsync(string url)
    {
        int exitCode = await StartupCommands.RunAsync([url]).ConfigureAwait(false);
        if (exitCode != 0)
        {
            Console.Error.WriteLine($"[UrlProtocol] 处理失败（exit={exitCode}）：{url}");
        }
        return true;
    }



    private static string GetUserConfigDirectory()
        => Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } config
            ? config
            : Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");



    private static string GetUserDataDirectory()
        => Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } data
            ? data
            : Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");



    private static string GetUserApplicationsDirectory()
        => Path.Join(GetUserDataDirectory(), "applications");



    private static void RunQuietly(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            process?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{fileName} {arguments}: {ex.Message}");
        }
    }



    /// <summary>从 mimeapps.list 的 <c>[Default Applications]</c> 里删掉本协议的关联。</summary>
    private static void RemoveMimeAssociation(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }
            string[] lines = File.ReadAllLines(path);
            var output = new List<string>(lines.Length);
            bool inDefaultSection = false;
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith('['))
                {
                    inDefaultSection = trimmed.Equals("[Default Applications]", StringComparison.OrdinalIgnoreCase);
                    output.Add(line);
                    continue;
                }
                if (inDefaultSection)
                {
                    int eq = trimmed.IndexOf('=');
                    if (eq > 0 && trimmed[..eq].Trim().Equals(MimeType, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                output.Add(line);
            }
            File.WriteAllLines(path, output);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


}
