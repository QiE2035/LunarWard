using System.Diagnostics;
using LunarWard.Platform;

namespace LunarWard.Wine;

/// <summary>
/// 从一个 <see cref="RunnerSource"/> 解析出可执行的 wine 二进制。
/// </summary>
/// <remarks>
/// 见 <c>docs/04-FEATURES.md</c> §3.2。
/// </remarks>
public sealed class RunnerResolver
{
    private readonly IXdgPaths _paths;

    private readonly ComponentRepository _components;


    public RunnerResolver(IXdgPaths paths, ComponentRepository components)
    {
        _paths = paths;
        _components = components;
    }


    /// <summary>
    /// 解析 runner。失败抛 <see cref="RuntimeNotFoundException"/>。
    /// </summary>
    public (string WineBinary, string RunnerDirectory, IReadOnlyDictionary<string, string> Env, string Explain)
        Resolve(RunnerSource source)
    {
        switch (source)
        {
            case RunnerSource.Managed managed:
                return ResolveManaged(managed);

            case RunnerSource.System:
                return ResolveSystem();

            case RunnerSource.Proton proton:
                return ResolveProton(proton);

            case RunnerSource.Custom custom:
                return ResolveCustom(custom);

            default:
                throw new RuntimeNotFoundException("?", $"未知 runner 来源 {source.GetType().Name}");
        }
    }


    private (string, string, IReadOnlyDictionary<string, string>, string) ResolveManaged(RunnerSource.Managed managed)
    {
        var dir = Path.Join(_paths.RunnersDirectory, managed.Component);
        var wine = Path.Join(dir, "bin", "wine");

        if (!File.Exists(wine))
        {
            // 组件索引里可能有自定义 files.wine 路径
            var found = _components.Find("wine", managed.Component);
            if (found is { Entry.Files.Wine: { } rel })
            {
                var alt = Path.Join(dir, rel);
                if (File.Exists(alt))
                {
                    wine = alt;
                }
                else
                {
                    throw new RuntimeNotFoundException(managed.Component, $"已安装目录里找不到 {rel}");
                }
            }
            else
            {
                throw new RuntimeNotFoundException(managed.Component, $"未安装：{dir}");
            }
        }

        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (_components.Find("wine", managed.Component) is { Entry.Features.Env: { } featuresEnv })
        {
            foreach (var (k, v) in featuresEnv)
            {
                env[k] = KeywordReplacer.Replace(v, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["build"] = dir,
                    ["prefix"] = null,
                    ["temp"] = null,
                    ["launcher"] = _paths.DataHome,
                });
            }
        }

        return (wine, dir, env, $"托管组件 {managed.Component}");
    }


    private (string, string, IReadOnlyDictionary<string, string>, string) ResolveSystem()
    {
        var wine = UnixProcessLauncher.Which("wine")
                   ?? new[] { "/usr/bin/wine", "/usr/local/bin/wine", "/usr/bin/wine64" }.FirstOrDefault(File.Exists)
                   ?? throw new RuntimeNotFoundException("System", "PATH 里找不到 wine");

        var root = Path.GetFullPath(Path.Join(Path.GetDirectoryName(wine), ".."));

        // 必须探测可用性：有的发行版 wine 包是空壳
        var version = ProbeVersion(wine);
        if (version is null)
        {
            throw new RuntimeNotFoundException("System", $"{wine} 无法执行（wine --version 失败）");
        }

        return (wine, root, new Dictionary<string, string>(), $"系统 wine {version} ({wine})");
    }


    private (string, string, IReadOnlyDictionary<string, string>, string) ResolveProton(RunnerSource.Proton proton)
    {
        var dir = Path.Join(proton.SteamLibraryPath, "steamapps", "common", proton.ProtonName);
        // 默认 WineBinary 模式（D9）：直接调 Proton 自带的 wine，不走 proton run 脚本。
        // Proton 9 及以前是 dist/bin/wine，Proton 10 / Experimental 改成 files/bin/wine。
        foreach (string rootName in (string[])["dist", "files"])
        {
            var wine = Path.Join(dir, rootName, "bin", "wine");
            if (File.Exists(wine))
            {
                return (wine, Path.Join(dir, rootName), new Dictionary<string, string>(),
                    $"Proton {proton.ProtonName} ({wine})");
            }
        }

        throw new RuntimeNotFoundException(proton.ProtonName,
            $"找不到 Proton wine 二进制：{Path.Join(dir, "dist", "bin", "wine")} 或 files/bin/wine");
    }


    private (string, string, IReadOnlyDictionary<string, string>, string) ResolveCustom(RunnerSource.Custom custom)
    {
        var path = custom.Path;
        string wine;

        if (Directory.Exists(path))
        {
            wine = Path.Join(path, "bin", "wine");
            if (!File.Exists(wine))
            {
                wine = Path.Join(path, "wine");
            }
        }
        else
        {
            wine = path;
        }

        if (!File.Exists(wine))
        {
            throw new RuntimeNotFoundException(path, $"自定义 runner 不存在：{wine}");
        }

        if ((File.GetUnixFileMode(wine) & UnixFileMode.UserExecute) == 0)
        {
            throw new RuntimeNotFoundException(path, $"自定义 runner 不可执行：{wine}");
        }

        return (wine, Path.GetDirectoryName(wine) ?? path, new Dictionary<string, string>(), $"自定义 {wine}");
    }


    /// <summary>执行 <c>wine --version</c>，返回版本字符串；失败返回 <c>null</c>。</summary>
    public static string? ProbeVersion(string wineBinary, TimeSpan? timeout = null)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = wineBinary,
                ArgumentList = { "--version" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit((int)(timeout ?? TimeSpan.FromSeconds(10)).TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                    // ignore
                }

                return null;
            }

            var text = (stdout + stderr).Trim();
            return process.ExitCode == 0 && text.Length > 0 ? text.Split('\n')[0].Trim() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
