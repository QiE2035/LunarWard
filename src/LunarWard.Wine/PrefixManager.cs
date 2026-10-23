namespace LunarWard.Wine;

/// <summary>
/// prefix 生命周期：创建、校验有效性、清理残留客户端。
/// </summary>
/// <remarks>见 <c>docs/03-LAUNCH-ENGINE.md</c> §3.1。</remarks>
public interface IPrefixManager
{
    /// <summary>prefix 是否已初始化（<c>system.reg</c> 存在）。</summary>
    bool IsValid(string prefix);

    /// <summary>必要时创建（调用 <c>wineboot -u</c>）；已有效则直接返回。</summary>
    Task InitializeAsync(string wineBinary, string prefix, IReadOnlyDictionary<string, string> env, CancellationToken ct = default);

    /// <summary>
    /// 清空 prefix：枚举客户端 → SIGTERM → 等 0.5s → SIGKILL → <c>wineserver -k</c>
    /// → 等最多 3s 确认清零。
    /// </summary>
    /// <returns>是否确认清零。</returns>
    Task<bool> CleanAsync(string prefix, string? wineBinary, CancellationToken ct = default);

    /// <summary>解析 prefix 下的用户目录（<c>drive_c/users/*</c>）。</summary>
    IReadOnlyList<string> EnumerateUserDirectories(string prefix);
}


/// <summary>默认实现。</summary>
public sealed class PrefixManager : IPrefixManager
{
    private readonly Platform.IProcessTree _processTree;

    private readonly Platform.IProcessController _controller;

    private readonly Platform.IProcessLauncher _launcher;

    private readonly Platform.IXdgPaths _paths;


    public PrefixManager(
        Platform.IProcessTree processTree,
        Platform.IProcessController controller,
        Platform.IProcessLauncher launcher,
        Platform.IXdgPaths paths)
    {
        _processTree = processTree;
        _controller = controller;
        _launcher = launcher;
        _paths = paths;
    }


    /// <summary>清理后等待清零的默认上限。</summary>
    public TimeSpan CleanTimeout { get; init; } = TimeSpan.FromSeconds(3);


    public bool IsValid(string prefix)
    {
        return !string.IsNullOrEmpty(prefix)
               && File.Exists(Path.Join(prefix, "system.reg"));
    }


    public async Task InitializeAsync(string wineBinary, string prefix, IReadOnlyDictionary<string, string> env, CancellationToken ct = default)
    {
        Directory.CreateDirectory(prefix);
        if (IsValid(prefix))
        {
            return;
        }

        var merged = new Dictionary<string, string>(env, StringComparer.Ordinal);
        merged.TryAdd("WINEPREFIX", prefix);

        var psi = new Platform.ProcessRequest
        {
            FileName = wineBinary,
            Arguments = ["wineboot", "-u"],
            WorkingDirectory = prefix,
            Environment = merged,
            NewSession = true,
        };

        using var process = _launcher.Start(psi);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // ignore
            }
        }
    }


    public async Task<bool> CleanAsync(string prefix, string? wineBinary, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return true;
        }

        var clients = _processTree.GetPrefixClients(prefix);
        if (clients.Count > 0)
        {
            foreach (var client in clients)
            {
                _controller.Signal(client.Pid, Platform.LinuxProcessController.SIGTERM);
            }

            await Task.Delay(500, ct).ConfigureAwait(false);

            foreach (var client in _processTree.GetPrefixClients(prefix))
            {
                // winedevice.exe 会忽略 SIGTERM，必须升级到 SIGKILL
                _controller.Signal(client.Pid, Platform.LinuxProcessController.SIGKILL);
            }
        }

        // wineserver -k 是权威做法：即使有漏网进程，它也会把整台 server 关掉
        var wineServer = wineBinary is null ? null : Path.Join(Path.GetDirectoryName(wineBinary), "wineserver");
        if (wineServer is not null && File.Exists(wineServer))
        {
            try
            {
                var psi = new Platform.ProcessRequest
                {
                    FileName = wineServer,
                    Arguments = ["-k"],
                    Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["WINEPREFIX"] = prefix },
                    NewSession = true,
                };
                using var process = _launcher.Start(psi);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // wineserver 不存在或超时都不致命，后面还有轮询确认
            }
        }
        else if (wineBinary is not null)
        {
            // 干净回退：wine wineserver -k
            try
            {
                var psi = new Platform.ProcessRequest
                {
                    FileName = wineBinary,
                    Arguments = ["wineserver", "-k"],
                    Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["WINEPREFIX"] = prefix },
                    NewSession = true,
                };
                using var process = _launcher.Start(psi);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // ignore
            }
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < CleanTimeout)
        {
            ct.ThrowIfCancellationRequested();
            if (_processTree.GetPrefixClients(prefix).Count == 0)
            {
                return true;
            }

            await Task.Delay(100, ct).ConfigureAwait(false);
        }

        return _processTree.GetPrefixClients(prefix).Count == 0;
    }


    public IReadOnlyList<string> EnumerateUserDirectories(string prefix)
    {
        var users = Path.Join(prefix, "drive_c", "users");
        if (!Directory.Exists(users))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateDirectories(users).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }
}
