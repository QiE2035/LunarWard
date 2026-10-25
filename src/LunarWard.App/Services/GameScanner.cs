using System.Text.Json;
using LunarWard.Games;
using LunarWard.Platform;

namespace LunarWard.App.Services;

/// <summary>扫描到的已安装游戏。</summary>
public sealed record DetectedGame(GameDefinition Definition, string Directory, string Source);


/// <summary>
/// 游戏库扫描（只读）。见 <c>docs/06-OPEN-QUESTIONS.md</c> Q6 的推荐：
/// 首期只做"扫描游戏目录"，不改别人的配置。
/// </summary>
/// <remarks>
/// 扫描范围：
/// <list type="bullet">
/// <item>用户自己配置过的目录（由调用方传入）</item>
/// <item><c>~/games/**</c>（本项目文档与本机实际布局）</item>
/// <item><c>~/.local/share/sleepy-launcher/config.json</c> 里记录的路径（只读）</item>
/// <item><c>~/.local/share/anime-game-launcher/**</c>（只读）</item>
/// </list>
/// </remarks>
public sealed class GameScanner
{
    private readonly IXdgPaths _paths;


    public GameScanner(IXdgPaths paths)
    {
        _paths = paths;
    }


    /// <summary>常见父目录。</summary>
    public IReadOnlyList<string> SearchRoots
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var roots = new List<string>
            {
                Path.Join(home, "games"),
                Path.Join(home, "Games"),
                Path.Join(home, "Genshin"),
                Path.Join(home, "ZenlessZoneZero"),
                Path.Join(home, ".local", "share", "sleepy-launcher"),
                Path.Join(home, ".local", "share", "anime-game-launcher"),
                Path.Join(home, ".local", "share", "lunarward", "games"),
            };

            // Steam 库：原神/星铁/绝区零在以 Steam 方式安装/接管时落在 steamapps/common 下。
            // 标准路径直接拼，非默认库从 libraryfolders.vdf 拿（与 Proton 扫描共用同一份解析）。
            roots.AddRange(SteamGameRoots(home));
            return roots;
        }
    }


    /// <summary>Steam 库里的 <c>steamapps/common</c> 目录（标准路径 + libraryfolders.vdf 的额外库）。</summary>
    private static IEnumerable<string> SteamGameRoots(string home)
    {
        yield return Path.Join(home, ".steam", "steam", "steamapps", "common");
        yield return Path.Join(home, ".local", "share", "Steam", "steamapps", "common");

        foreach (var library in RuntimeScanner.SteamLibraries())
        {
            yield return Path.Join(library, "steamapps", "common");
        }
    }


    /// <summary>执行扫描（深度受限，避免扫整个 home）。</summary>
    public IReadOnlyList<DetectedGame> Scan(int maxDepth = 3)
    {
        var result = new Dictionary<string, DetectedGame>(StringComparer.Ordinal);

        foreach (var root in SearchRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            ScanDirectory(root, 0, maxDepth, result);
        }

        foreach (var (dir, path) in ReadOtherLauncherPaths())
        {
            TryAdd(dir, path, result);
        }

        return result.Values.OrderBy(x => x.Definition.Kind).ThenBy(x => x.Directory).ToList();
    }


    /// <summary>
    /// 只扫一个根目录（自检/单测用；<see cref="Scan"/> 是它 + 已知来源的合并）。
    /// </summary>
    public IReadOnlyList<DetectedGame> ScanRoot(string root, int maxDepth = 3)
    {
        var result = new Dictionary<string, DetectedGame>(StringComparer.Ordinal);
        if (Directory.Exists(root))
        {
            ScanDirectory(root, 0, maxDepth, result);
        }

        return result.Values.OrderBy(x => x.Definition.Kind).ThenBy(x => x.Directory).ToList();
    }


    private void ScanDirectory(string dir, int depth, int maxDepth, Dictionary<string, DetectedGame> result)
    {
        if (depth > maxDepth)
        {
            return;
        }

        try
        {
            // 命中就记录，并继续向下（其它游戏可能在子目录）
            var definition = GameCatalog.Detect(dir);
            if (definition is not null)
            {
                TryAdd(definition.Kind + "|" + definition.Edition, dir, result, explicitDefinition: definition);
            }

            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                // 跳过明显无关的大目录
                if (name is "prefix" or "prefixes" or "runners" or "dxvks" or "components"
                    or "steamapps" or "compatdata" or "node_modules" or ".git")
                {
                    continue;
                }

                ScanDirectory(sub, depth + 1, maxDepth, result);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // 跳过无权限目录
        }
        catch (IOException)
        {
            // 跳过
        }
    }


    private void TryAdd(string? key, string directory, Dictionary<string, DetectedGame> result,
        GameDefinition? explicitDefinition = null)
    {
        if (key is null)
        {
            return;
        }

        if (result.ContainsKey(key))
        {
            return;
        }

        var definition = explicitDefinition ?? GameCatalog.Detect(directory);
        if (definition is null)
        {
            return;
        }

        result[key] = new DetectedGame(definition, directory, "扫描");
    }


    /// <summary>
    /// 只读地从别的启动器配置里读游戏目录。**不修改任何东西**。
    /// </summary>
    private IEnumerable<(string Key, string Path)> ReadOtherLauncherPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configPath = Path.Join(home, ".local", "share", "sleepy-launcher", "config.json");
        if (!File.Exists(configPath))
        {
            yield break;
        }

        List<string>? paths = null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            var found = new List<string>();
            CollectPaths(document.RootElement, found, 0);
            paths = found;
        }
        catch (JsonException)
        {
            // 配置损坏：静默跳过（只读，不改）
        }

        if (paths is null)
        {
            yield break;
        }

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var expanded = ProfileConfigExpand(path);
            if (!Directory.Exists(expanded))
            {
                continue;
            }

            var definition = GameCatalog.Detect(expanded);
            if (definition is not null)
            {
                yield return (definition.Kind + "|" + definition.Edition, expanded);
            }
        }
    }


    private static void CollectPaths(JsonElement element, List<string> result, int depth)
    {
        if (depth > 6)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name is "path" or "game_path" or "install_path" or "game_dir"
                        && property.Value.ValueKind == JsonValueKind.String)
                    {
                        result.Add(property.Value.GetString() ?? "");
                    }
                    else
                    {
                        CollectPaths(property.Value, result, depth + 1);
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectPaths(item, result, depth + 1);
                }

                break;
        }
    }


    private static string ProfileConfigExpand(string path)
    {
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.GetFullPath(home + path[1..]);
        }

        return Path.GetFullPath(path);
    }
}


/// <summary>可用的 wine 运行时（用于 UI 下拉选择）。</summary>
public sealed record AvailableRuntime(string Name, string Source, string Path, LunarWard.Wine.RunnerSource Runner, string? Version);


/// <summary>
/// 运行时发现：系统 wine、本项目托管的 runner、Steam Proton、
/// 以及**只读**借用其它启动器已下载的 runner。
/// </summary>
public sealed class RuntimeScanner
{
    private readonly IXdgPaths _paths;


    public RuntimeScanner(IXdgPaths paths)
    {
        _paths = paths;
    }


    public IReadOnlyList<AvailableRuntime> Scan()
    {
        var result = new List<AvailableRuntime>();

        // 1) 系统 wine
        var system = UnixProcessLauncher.Which("wine");
        if (system is not null)
        {
            result.Add(new AvailableRuntime(
                $"System · {LunarWard.Wine.RunnerResolver.ProbeVersion(system) ?? "wine"}",
                "System", system, new LunarWard.Wine.RunnerSource.System(), null));
        }

        // 2) 本项目管理目录
        foreach (var dir in SafeDirectories(_paths.RunnersDirectory))
        {
            var wine = Path.Join(dir, "bin", "wine");
            if (File.Exists(wine))
            {
                result.Add(new AvailableRuntime(
                    Path.GetFileName(dir), "Managed", wine,
                    new LunarWard.Wine.RunnerSource.Managed(Path.GetFileName(dir)),
                    Wine.RunnerResolver.ProbeVersion(wine)));
            }
        }

        // 3) 其它启动器已下载的 runner（只读借用：注册为 Custom）
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var borrowedRoot = Path.Join(home, ".local", "share", "sleepy-launcher", "runners");
        foreach (var dir in SafeDirectories(borrowedRoot))
        {
            var wine = Path.Join(dir, "bin", "wine");
            if (File.Exists(wine))
            {
                result.Add(new AvailableRuntime(
                    $"借用 · {Path.GetFileName(dir)}", "Borrowed", wine,
                    new LunarWard.Wine.RunnerSource.Custom(wine),
                    Wine.RunnerResolver.ProbeVersion(wine)));
            }
        }

        // 4) Steam Proton
        foreach (var library in SteamLibraries())
        {
            foreach (var dir in SafeDirectories(Path.Join(library, "steamapps", "common")))
            {
                var name = Path.GetFileName(dir);
                if (!File.Exists(Path.Join(dir, "dist", "bin", "wine")))
                {
                    continue;
                }

                result.Add(new AvailableRuntime(
                    $"Proton · {name}", "Proton", Path.Join(dir, "dist", "bin", "wine"),
                    new LunarWard.Wine.RunnerSource.Proton(library, name),
                    Wine.RunnerResolver.ProbeVersion(Path.Join(dir, "dist", "bin", "wine"))));
            }
        }

        return result;
    }


    /// <summary>Steam 库目录（标准路径 + libraryfolders.vdf 里的 <c>path</c>）。</summary>
    public static IReadOnlyList<string> SteamLibraries()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Join(home, ".steam", "steam"),
            Path.Join(home, ".local", "share", "Steam"),
        };

        var result = new List<string>();
        foreach (var root in candidates)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            result.Add(root);

            var vdf = Path.Join(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
            {
                continue;
            }

            // 不引入 VDF 解析库（docs/06 Q2 的退化方案）：只抓 `"path"  "<dir>"` 行。
            // 足以覆盖 libraryfolders.vdf 的两种格式（新旧都在同一行里给 path）。
            try
            {
                foreach (var line in File.ReadLines(vdf))
                {
                    var index = line.IndexOf("\"path\"", StringComparison.Ordinal);
                    if (index < 0)
                    {
                        continue;
                    }

                    var rest = line[(index + 6)..];
                    var first = rest.IndexOf('"');
                    var last = rest.LastIndexOf('"');
                    if (first < 0 || last <= first)
                    {
                        continue;
                    }

                    var path = rest[(first + 1)..last].Replace("\\\\", "\\", StringComparison.Ordinal);
                    if (Directory.Exists(path) && !result.Contains(path))
                    {
                        result.Add(path);
                    }
                }
            }
            catch (IOException)
            {
                // 跳过
            }
        }

        return result;
    }


    private static IEnumerable<string> SafeDirectories(string root)
    {
        try
        {
            return Directory.Exists(root) ? Directory.EnumerateDirectories(root).OrderBy(x => x).ToList() : [];
        }
        catch (Exception)
        {
            return [];
        }
    }
}
