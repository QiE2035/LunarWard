using LunarWard.Games;
using LunarWard.Launch;
using LunarWard.Platform;

namespace LunarWard.App.Services;

/// <summary>
/// 命令行子命令（对应上游 <c>App.xaml.cs</c> 的 <c>HandleStartupAsync</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 上游支持 <c>starward startgame --biz hk4e_cn</c>（桌面快捷方式/外部程序直接启动游戏）、
/// <c>rpc</c>（Discord Rich Presence，未移植）、<c>playtime --pid</c>（游戏退出后补记账）、
/// <c>starward://</c> 协议。Linux 上桌面入口同样需要「不打开窗口、直接启动游戏」的入口，
/// 所以先移植 <c>startgame</c>；其余子命令仍按缺口记在 parity 文档里。
/// </para>
/// <para>
/// 这里不创建 Avalonia 应用：<see cref="AppServices"/> 可以在任何线程构建，
/// 启动完游戏就退出，窗口完全不开。
/// </para>
/// </remarks>
internal static class StartupCommands
{
    /// <summary>是否是本类处理的子命令（含 <c>lunarward://</c> 协议 URL）。</summary>
    public static bool IsCommand(string[] args)
        => args.Length > 0
           && (args[0].Equals("startgame", StringComparison.OrdinalIgnoreCase)
               || args[0].Equals("playtime", StringComparison.OrdinalIgnoreCase)
               || args[0].StartsWith("lunarward://", StringComparison.OrdinalIgnoreCase));


    /// <summary>
    /// <c>startgame --biz &lt;hk4e_cn|hk4e_global|hkrpg_*|nap_*&gt;</c>：
    /// 找到对应区服的已配置档案，按启动器的同一条路径启动游戏，不打开窗口。
    /// </summary>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args[0].StartsWith("lunarward://", StringComparison.OrdinalIgnoreCase))
        {
            return await RunUrlAsync(args[0]).ConfigureAwait(false);
        }

        if (args[0].Equals("playtime", StringComparison.OrdinalIgnoreCase))
        {
            return await RunPlayTimeAsync(args).ConfigureAwait(false);
        }

        string? biz = ReadArgument(args, "--biz");
        if (string.IsNullOrWhiteSpace(biz))
        {
            Console.Error.WriteLine("用法：lunarward startgame --biz <hk4e_cn|hk4e_global|hkrpg_cn|hkrpg_global|nap_cn|nap_global>");
            return 2;
        }

        return await StartByBizAsync(biz).ConfigureAwait(false);
    }


    /// <summary>
    /// <c>lunarward://startgame/&lt;game_biz&gt;</c>（对应上游 <c>UrlProtocolService.HandleUrlProtocolAsync</c>）。
    /// </summary>
    /// <remarks>
    /// 桌面文件已经把 <c>x-scheme-handler/lunarward</c> 注册给本程序；这里只处理
    /// <c>startgame</c>（启动游戏）与 <c>playtime</c>（未移植，明确报错而不静默失败）。
    /// </remarks>
    private static async Task<int> RunUrlAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            Console.Error.WriteLine($"无法解析 URL：{url}");
            return 2;
        }

        string biz = uri.AbsolutePath.Trim('/');
        if (uri.Host.Equals("startgame", StringComparison.OrdinalIgnoreCase))
        {
            return await StartByBizAsync(biz).ConfigureAwait(false);
        }

        if (uri.Host.Equals("playtime", StringComparison.OrdinalIgnoreCase))
        {
            // lunarward://playtime?biz=hk4e_cn&pid=1234 —— 与 CLI 的 playtime 子命令同一实现
            var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0)
                {
                    query[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..]);
                }
            }

            string bizFromQuery = query.GetValueOrDefault("biz", "");
            string pidFromQuery = query.GetValueOrDefault("pid", "");
            return await RunPlayTimeAsync(["playtime", "--biz", bizFromQuery, "--pid", pidFromQuery]).ConfigureAwait(false);
        }

        Console.Error.WriteLine($"不支持的 URL：{url}");
        return 2;
    }


    private static async Task<int> StartByBizAsync(string biz)
    {
        try
        {
            var paths = XdgPaths.Default;
            AppServices.Initialize(paths);
            var services = AppServices.Current;

            (string Id, ProfileConfig Profile)? match = null;
            foreach ((string id, ProfileConfig profile) in services.Config.Profiles)
            {
                GameDefinition? definition = GameCatalog.Get(
                    profile.Game,
                    string.Equals(profile.Edition, "Global", StringComparison.OrdinalIgnoreCase)
                        ? GameEdition.Global
                        : GameEdition.China);
                if (definition is not null
                    && string.Equals(definition.GameBiz, biz, StringComparison.OrdinalIgnoreCase))
                {
                    match = (id, profile);
                    break;
                }
            }

            if (match is null)
            {
                Console.Error.WriteLine($"没有区服为 {biz} 的档案（先运行 LunarWard 配好游戏目录）");
                return 3;
            }

            LaunchResult result = await services.LauncherService
                .StartAsync(match.Value.Id, match.Value.Profile)
                .ConfigureAwait(false);
            if (result.Success)
            {
                Console.WriteLine($"STARTED {biz} pid={services.LauncherService.GamePid}");
                return 0;
            }

            Console.Error.WriteLine(result.Error?.Message ?? "启动失败");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }


    /// <summary>
    /// <c>playtime --biz &lt;biz&gt; --pid &lt;pid&gt;</c>：盯着外部启动的游戏进程，
    /// 退出后按「进程开始 → 退出」补一条游戏时长（上游 <c>playtime</c> 子命令的语义）。
    /// </summary>
    private static async Task<int> RunPlayTimeAsync(string[] args)
    {
        string? biz = ReadArgument(args, "--biz");
        string? pidText = ReadArgument(args, "--pid");
        if (string.IsNullOrWhiteSpace(biz) || !int.TryParse(pidText, out int pid) || pid <= 0)
        {
            Console.Error.WriteLine("用法：lunarward playtime --biz <biz> --pid <pid>");
            return 2;
        }

        try
        {
            AppServices.Initialize(XdgPaths.Default);
            var services = AppServices.Current;

            // 同一个 pid 只允许一个 watcher（上游用 AppInstance 单例锁）
            string lockPath = Path.Join(
                services.Paths.CacheHome, "runtime", $"playtime-{pid}.lock");
            Directory.CreateDirectory(Path.Join(services.Paths.CacheHome, "runtime"));
            FileStream? guard = null;
            try
            {
                guard = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                Console.Error.WriteLine($"pid {pid} 已有 playtime watcher 在记录");
                return 0;
            }

            using (guard)
            {
                System.Diagnostics.Process process;
                try
                {
                    process = System.Diagnostics.Process.GetProcessById(pid);
                }
                catch (ArgumentException)
                {
                    Console.Error.WriteLine($"pid {pid} 已退出，无法补记时长");
                    return 3;
                }

                DateTimeOffset started = TryStartTime(pid) ?? DateTimeOffset.Now;
                await process.WaitForExitAsync().ConfigureAwait(false);
                DateTimeOffset exited = DateTimeOffset.Now;
                process.Dispose();

                services.PlayTime.RecordSession(biz, pid, started, exited);
                Console.WriteLine($"PLAYTIME {biz} pid={pid} {(exited - started).TotalMinutes:F1}min");
            }

            try
            {
                File.Delete(lockPath);
            }
            catch (IOException)
            {
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }


    /// <summary>进程开始时间（/proc 读不到时返回 null，退回 now）。</summary>
    private static DateTimeOffset? TryStartTime(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return new DateTimeOffset(process.StartTime);
        }
        catch (Exception)
        {
            return null;
        }
    }


    private static string? ReadArgument(string[] args, string name)
    {
        int at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
