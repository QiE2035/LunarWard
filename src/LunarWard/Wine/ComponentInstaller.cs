using System.Formats.Tar;
using System.Net;
using System.Net.Http.Headers;
using System.IO.Compression;
using System.Security.Cryptography;
using LunarWard.Platform;

namespace LunarWard.Wine;

/// <summary>组件下载进度。</summary>
public sealed record ComponentProgress(string Name, long ReceivedBytes, long? TotalBytes)
{
    public double? Percent => TotalBytes is > 0 ? (double)ReceivedBytes / TotalBytes.Value : null;
}


/// <summary>
/// 托管组件（wine runner / dxvk）的下载与安装。
/// </summary>
/// <remarks>
/// <para>
/// 见 <c>docs/04-FEATURES.md</c> §3.3：复用 an-anime-team 的组件索引格式，
/// 索引目录为 <c>&lt;data&gt;/components/</c>。
/// </para>
/// <para>
/// <b>与计划的一处偏差</b>：计划用 <c>git clone/fetch + reset --hard</c> 同步索引，
/// 但引入 git 子系统（或调用外部 git）会让"没有 git"的用户直接不可用。
/// 这里改为 <b>直接下载 components.json 与分组 JSON</b>（raw.githubusercontent.com），
/// 语义等价且零外部依赖；需要 git 行为的用户仍可手动往目录里放索引文件。
/// </para>
/// <para>
/// 安装策略：
/// </para>
/// <list type="bullet">
/// <item>下载到 <c>&lt;cache&gt;/downloads/&lt;file&gt;.part</c>，校验后再 rename（缓存永远完整）</item>
/// <item>解压到 <c>&lt;dest&gt;.tmp</c>，成功后 rename 到 <c>&lt;dest&gt;</c>（避免半装）</item>
/// <item>失败时保留 <c>.part</c> 供续传，但绝不 rename 出半个组件</item>
/// </list>
/// </remarks>
public sealed class ComponentInstaller
{
    /// <summary>索引的原始下载地址（an-anime-team/components）。</summary>
    public const string IndexBaseUrl = "https://raw.githubusercontent.com/an-anime-team/components/main";


    private readonly IXdgPaths _paths;

    private readonly HttpClient _httpClient;

    private readonly ComponentRepository _repository;


    public ComponentInstaller(IXdgPaths paths, HttpClient? httpClient = null)
    {
        _paths = paths;
        _httpClient = httpClient ?? CreateDefaultClient();
        _repository = new ComponentRepository(paths.ComponentsDirectory);
    }


    public ComponentRepository Repository => _repository;


    private static HttpClient CreateDefaultClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            EnableMultipleHttp2Connections = true,
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(30),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
    }


    /// <summary>下载/更新组件索引（失败的组不阻断其它组）。</summary>
    public async Task<ComponentIndexUpdateResult> UpdateIndexAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(_paths.ComponentsDirectory);
        var errors = new List<string>();
        var updated = 0;

        // 1) components.json
        var indexText = await TryGetStringAsync($"{IndexBaseUrl}/components.json", ct).ConfigureAwait(false);
        if (indexText is null)
        {
            return new ComponentIndexUpdateResult(0, ["components.json 下载失败"]);
        }

        AtomicFile.WriteAllText(Path.Join(_paths.ComponentsDirectory, "components.json"), indexText);
        updated++;

        // 2) 每个分组
        ComponentIndex index;
        try
        {
            index = System.Text.Json.JsonSerializer.Deserialize<ComponentIndex>(indexText,
                       new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new ComponentIndex();
        }
        catch (System.Text.Json.JsonException)
        {
            return new ComponentIndexUpdateResult(updated, ["components.json 解析失败"]);
        }

        foreach (var (kind, groups) in new[]
                 {
                     ("wine", index.Wine),
                     ("dxvk", index.Dxvk),
                 })
        {
            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"{kind}/{group.Name}");
                var text = await TryGetStringAsync($"{IndexBaseUrl}/{kind}/{group.Name}.json", ct).ConfigureAwait(false);
                if (text is null)
                {
                    errors.Add($"{kind}/{group.Name}.json 下载失败");
                    continue;
                }

                var dir = Path.Join(_paths.ComponentsDirectory, kind);
                Directory.CreateDirectory(dir);
                AtomicFile.WriteAllText(Path.Join(dir, group.Name + ".json"), text);
                updated++;
            }
        }

        return new ComponentIndexUpdateResult(updated, errors);
    }


    /// <summary>
    /// 下载并安装一个 wine runner。
    /// </summary>
    public async Task<string> InstallWineAsync(
        string componentName,
        IProgress<ComponentProgress>? progress = null,
        CancellationToken ct = default)
    {
        var found = _repository.Find("wine", componentName)
                    ?? throw new RuntimeNotFoundException(componentName, "组件索引里没有这个 wine");

        var target = Path.Join(_paths.RunnersDirectory, componentName);
        await DownloadAndExtractAsync(found.Entry, target, progress, ct).ConfigureAwait(false);
        return target;
    }


    /// <summary>下载并安装一个 dxvk。</summary>
    public async Task<string> InstallDxvkAsync(
        string componentName,
        IProgress<ComponentProgress>? progress = null,
        CancellationToken ct = default)
    {
        var found = _repository.Find("dxvk", componentName)
                    ?? throw new RuntimeNotFoundException(componentName, "组件索引里没有这个 dxvk");

        var target = Path.Join(_paths.DxvkDirectory, componentName);
        await DownloadAndExtractAsync(found.Entry, target, progress, ct).ConfigureAwait(false);
        return target;
    }


    /// <summary>该组件是否已安装。</summary>
    public bool IsInstalled(string kind, string componentName)
    {
        var root = kind == "wine" ? _paths.RunnersDirectory : _paths.DxvkDirectory;
        var dir = Path.Join(root, componentName);
        if (!Directory.Exists(dir))
        {
            return false;
        }

        // wine 必须真有 bin/wine（空目录不算）
        return kind != "wine" || File.Exists(Path.Join(dir, "bin", "wine"));
    }


    private async Task DownloadAndExtractAsync(
        ComponentEntry entry,
        string target,
        IProgress<ComponentProgress>? progress,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(entry.Uri))
        {
            throw new InvalidOperationException($"组件 {entry.Name} 没有下载地址");
        }

        var downloads = Path.Join(_paths.CacheHome, "downloads");
        Directory.CreateDirectory(downloads);
        var fileName = Path.GetFileName(new Uri(entry.Uri).AbsolutePath);
        var archive = Path.Join(downloads, fileName);

        await DownloadAsync(entry.Uri, archive, entry.Name, progress, ct).ConfigureAwait(false);

        // 先解压到 .tmp，成功后 rename
        var temp = target + ".tmp";
        if (Directory.Exists(temp))
        {
            Directory.Delete(temp, recursive: true);
        }

        try
        {
            Directory.CreateDirectory(temp);
            Extract(archive, temp);
            FlattenSingleRoot(temp);
            MarkExecutable(temp);

            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }

            Directory.Move(temp, target);
        }
        catch (Exception)
        {
            try
            {
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
            catch (Exception)
            {
                // ignore
            }

            throw;
        }
    }


    private async Task DownloadAsync(
        string uri,
        string path,
        string name,
        IProgress<ComponentProgress>? progress,
        CancellationToken ct)
    {
        // 已存在且非空 → 视为已下载（组件包不带校验和，上游也没有）
        if (File.Exists(path) && new FileInfo(path).Length > 0)
        {
            progress?.Report(new ComponentProgress(name, new FileInfo(path).Length, new FileInfo(path).Length));
            return;
        }

        var part = path + ".part";

        // 断点续传：上次失败留下的 .part 用 Range 续上。组件包动辄上百 MB，
        // 走 raw.githubusercontent.com 时偶发断流，从头下很浪费。
        long startOffset = File.Exists(part) ? new FileInfo(part).Length : 0;
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (startOffset > 0)
        {
            request.Headers.Range = new RangeHeaderValue(startOffset, null);
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        bool resumed = response.StatusCode == HttpStatusCode.PartialContent && startOffset > 0;
        if (!resumed)
        {
            startOffset = 0;
        }

        long? total = response.Content.Headers.ContentLength is { } length
            ? length + startOffset
            : null;

        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var destination = new FileStream(part, resumed ? FileMode.Append : FileMode.Create,
                         FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
        {
            var buffer = new byte[1 << 16];
            long received = startOffset;
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                received += read;
                progress?.Report(new ComponentProgress(name, received, total));
            }

            if (total is { } expected && received < expected)
            {
                throw new IOException($"下载不完整：{received}/{expected} 字节（可用同一命令续传）");
            }
        }

        File.Move(part, path, overwrite: true);
    }


    /// <summary>
    /// 有的组件包自带一层与组件同名的根目录（<c>name/bin/wine</c>），解压后就不再有
    /// <c>bin/wine</c>。这里把「只有一个子目录且其中才有 bin/wine」的情况摊平一层。
    /// </summary>
    private static void FlattenSingleRoot(string dir)
    {
        if (File.Exists(Path.Join(dir, "bin", "wine")))
        {
            return;
        }

        string[] entries = Directory.GetFileSystemEntries(dir);
        if (entries.Length != 1 || !Directory.Exists(entries[0]) || !File.Exists(Path.Join(entries[0], "bin", "wine")))
        {
            return;
        }

        foreach (string child in Directory.GetFileSystemEntries(entries[0]))
        {
            Directory.Move(child, Path.Join(dir, Path.GetFileName(child)));
        }

        Directory.Delete(entries[0]);
    }


    /// <summary>
    /// 解压组件包。支持 <c>.tar.xz</c> / <c>.tar.gz</c> / <c>.tar.zst</c> / <c>.zip</c> / <c>.tar</c>。
    /// </summary>
    /// <remarks>
    /// <b>为什么用外部 tar/unzip 而不是引库</b>：BCL 只有 gzip 与 zip，
    /// <c>.tar.xz</c>（这是 an-anime-team 的 wine 组件默认格式）需要 xz 解码器。
    /// 引入 <c>SharpCompress</c> 能解 xz，但会让"解一个 xz"多背一个包；
    /// 而 <c>tar</c> / <c>unzip</c> 是所有 Linux 发行版的 base 组件（POSIX 要求）。
    /// BCL 的 <see cref="TarReader"/> 作为无 tar 环境的退路。
    /// </remarks>
    private static void Extract(string archive, string destination)
    {
        var name = archive.ToLowerInvariant();

        if (name.EndsWith(".zip", StringComparison.Ordinal))
        {
            // BCL 的 ZipFile 足够，且可防目录穿越
            ZipFile.ExtractToDirectory(archive, destination, overwriteFiles: true);
            return;
        }

        var tar = Which("tar");
        if (tar is not null)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = tar,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-x");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(archive);
            psi.ArgumentList.Add("-C");
            psi.ArgumentList.Add(destination);
            // --no-same-owner：普通用户解包时不要试图 chown
            psi.ArgumentList.Add("--no-same-owner");

            using var process = System.Diagnostics.Process.Start(psi)
                                ?? throw new InvalidOperationException("无法启动 tar");
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidDataException($"tar 解包失败（退出码 {process.ExitCode}）：{stderr.Trim()}");
            }

            return;
        }

        // 无 tar：只处理纯 .tar（BCL 能读）；其它压缩格式给出明确错误
        if (name.EndsWith(".tar", StringComparison.Ordinal))
        {
            using var file = File.OpenRead(archive);
            ExtractTar(file, destination);
            return;
        }

        throw new NotSupportedException(
            $"系统缺少 tar，无法解压 {Path.GetFileName(archive)}。请安装 tar（几乎所有发行版都自带）。");
    }


    private static string? Which(string executable)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin").Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Join(dir, executable);
            if (File.Exists(candidate) && (File.GetUnixFileMode(candidate) & UnixFileMode.UserExecute) != 0)
            {
                return candidate;
            }
        }

        return null;
    }


    private static void ExtractTar(Stream stream, string destination)
    {
        using var reader = new TarReader(stream, leaveOpen: true);
        while (reader.GetNextEntry() is { } entry)
        {
            var target = Path.GetFullPath(Path.Join(destination, entry.Name));

            // 防目录穿越
            if (!target.StartsWith(Path.GetFullPath(destination), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"组件包含非法路径：{entry.Name}");
            }

            if (entry.EntryType is TarEntryType.Directory)
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }


    /// <summary>给 bin/ 下的文件加执行位（tar 里通常已带，但 zip 不带）。</summary>
    private static void MarkExecutable(string root)
    {
        var bin = Path.Join(root, "bin");
        if (!Directory.Exists(bin))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(bin))
        {
            try
            {
                var mode = File.GetUnixFileMode(file);
                File.SetUnixFileMode(file,
                    mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }
            catch (Exception)
            {
                // ignore
            }
        }
    }


    private async Task<string?> TryGetStringAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}


/// <summary>索引更新的结果。</summary>
public sealed record ComponentIndexUpdateResult(int UpdatedFiles, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
}
