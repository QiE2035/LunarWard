using System.Diagnostics;
using System.IO.Compression;

namespace LunarWard.Installer;

/// <summary>安装器无法在当前环境完成某一步时抛出。</summary>
public sealed class InstallerCapabilityException : Exception
{
    public InstallerCapabilityException(string message) : base(message)
    {
    }

    public InstallerCapabilityException(string message, Exception? inner) : base(message, inner)
    {
    }
}


/// <summary>
/// 压缩包解压抽象。
///
/// <para>上游用 <c>SharpSevenZip.SharpSevenZipExtractor</c>（内置 7z.dll 的托管壳）。
/// <c>SharpSevenZip</c> 的 native 部分只有 Windows 二进制，Linux 上不可用，
/// 且本项目不允许再加包，因此改为：</para>
/// <list type="number">
/// <item><description>优先调用系统的 <c>7zz</c> / <c>7z</c> / <c>7za</c>（p7zip / 7-Zip 官方 Linux 版都提供）；</description></item>
/// <item><description>压缩包是普通 zip 且系统没有 7z 时，回退到 BCL 的 <see cref="ZipFile"/>；</description></item>
/// <item><description>两者都不可用时抛 <see cref="InstallerCapabilityException"/>，消息里写明缺什么。</description></item>
/// </list>
///
/// <para><b>多卷包</b>：上游把若干个包文件串成 <see cref="FileCombinedStream"/> 再交给
/// <c>SharpSevenZipExtractor</c>，等价于"分卷 7z 直接拼接"。这里保留该语义：
/// 若给定多个文件，先尝试按 7-Zip 分卷命名（<c>*.7z.001</c>）直接解第一段，
/// 否则把内容拼接进临时文件再解。</para>
/// </summary>
public interface IArchiveExtractor
{
    /// <summary>
    /// 解压到 <paramref name="destination"/>。
    /// <paramref name="percent"/> 报告 0..1 的进度（对应上游 <c>Extracting</c> 事件的 <c>FinishPercent</c>）。
    /// </summary>
    Task ExtractAsync(IReadOnlyList<string> archives, string destination, IProgress<double>? percent, CancellationToken cancellationToken = default);
}


/// <summary>
/// 基于外部 7z 可执行文件的实现（默认）。
/// </summary>
public sealed class SevenZipArchiveExtractor : IArchiveExtractor
{
    /// <summary>允许通过环境变量指定 7z 路径，便于打包时固定。</summary>
    public const string ExecutableEnvironmentVariable = "LUNARWARD_7Z";

    private static readonly string[] CandidateNames = ["7zz", "7z", "7za"];


    private readonly string? _executable;

    private readonly IInstallerLogger _logger;


    public SevenZipArchiveExtractor(IInstallerLogger? logger = null)
    {
        _logger = logger ?? NullInstallerLogger.Instance;
        _executable = FindExecutable();
    }


    /// <summary>找到的 7z 可执行文件路径；找不到时为 <c>null</c>。</summary>
    public string? Executable => _executable;


    public async Task ExtractAsync(IReadOnlyList<string> archives, string destination, IProgress<double>? percent, CancellationToken cancellationToken = default)
    {
        if (archives.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(destination);

        if (_executable is null)
        {
            await ExtractZipFallbackAsync(archives, destination, percent, cancellationToken);
            return;
        }

        var (archivePath, temporary) = PrepareArchive(archives);
        try
        {
            await ExtractWithSevenZipAsync(archivePath, destination, percent, cancellationToken);
        }
        finally
        {
            if (temporary is not null && File.Exists(temporary))
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception)
                {
                    // 临时文件删不掉不影响结果
                }
            }
        }
    }




    /// <summary>
    /// 选出要交给 7z 的路径。
    /// 单个文件直接用；多个文件若能组成 7-Zip 分卷序列，用第一段；
    /// 否则拼接为临时文件（等价于上游的 <c>FileCombinedStream</c>）。
    /// </summary>
    private (string Path, string? Temporary) PrepareArchive(IReadOnlyList<string> archives)
    {
        if (archives.Count == 1)
        {
            return (archives[0], null);
        }

        if (LooksLikeSplitVolumeSet(archives, out var firstVolume))
        {
            _logger.LogDebug("Detected split 7z volume set, using first volume: {volume}", firstVolume);
            return (firstVolume, null);
        }

        var temporary = Path.Join(Path.GetTempPath(), $"lunarward-{Guid.NewGuid():N}.archive");
        _logger.LogDebug("Concatenating {count} archive parts into {path}", archives.Count, temporary);
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20))
        {
            foreach (var archive in archives)
            {
                using var input = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20);
                input.CopyTo(output);
            }
        }

        return (temporary, temporary);
    }


    /// <summary>
    /// 判断这组文件是不是 7z 的分卷序列（<c>name.7z.001</c>, <c>name.7z.002</c>, ...），
    /// 且连续、无缺号。7z 二进制接受第一段即可自动续读后续卷。
    /// </summary>
    private static bool LooksLikeSplitVolumeSet(IReadOnlyList<string> archives, out string firstVolume)
    {
        firstVolume = archives[0];
        if (!archives[0].EndsWith(".001", StringComparison.Ordinal))
        {
            return false;
        }

        var baseName = archives[0][..^4];
        for (var i = 1; i <= archives.Count - 1; i++)
        {
            var expected = $"{baseName}{i + 1:000}";
            if (!archives.Any(x => x.EndsWith(Path.GetFileName(expected), StringComparison.Ordinal)))
            {
                return false;
            }
        }

        // 第一段必须确实存在，且其余卷的目录与它相同
        var directory = Path.GetDirectoryName(Path.GetFullPath(archives[0]));
        var actualFirst = archives.FirstOrDefault(x => x.EndsWith(".001", StringComparison.Ordinal));
        if (actualFirst is null || Path.GetDirectoryName(Path.GetFullPath(actualFirst)) != directory)
        {
            return false;
        }

        firstVolume = actualFirst;
        return true;
    }


    private async Task ExtractWithSevenZipAsync(string archive, string destination, IProgress<double>? percent, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executable!,
            // -bsp1：进度写 stdout（上游 SharpSevenZip 的 FinishPercent 等价物）
            // -bso0/-bse1：不输出列表，错误仍走 stderr
            // -y：覆盖已存在文件（上游 ExtractArchive 也是覆盖）
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("x");
        startInfo.ArgumentList.Add(archive);
        startInfo.ArgumentList.Add($"-o{destination}");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-bsp1");
        startInfo.ArgumentList.Add("-bso1");
        startInfo.ArgumentList.Add("-bse1");

        using var process = new Process { StartInfo = startInfo };
        var errorBuffer = new System.Text.StringBuilder();

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (errorBuffer)
                {
                    errorBuffer.AppendLine(e.Data);
                }
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        var lastPercent = -1d;
        try
        {
            var buffer = new char[512];
            int read;
            while ((read = await process.StandardOutput.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (percent is null)
                {
                    continue;
                }

                var value = ParseLastPercent(buffer.AsSpan(0, read));
                if (value is not null && value.Value > lastPercent)
                {
                    lastPercent = value.Value;
                    percent.Report(Math.Clamp(lastPercent, 0, 1));
                }
            }

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            string error;
            lock (errorBuffer)
            {
                error = errorBuffer.ToString().Trim();
            }

            throw new InvalidOperationException(
                $"7z exited with code {process.ExitCode} while extracting '{archive}' to '{destination}'." +
                (string.IsNullOrWhiteSpace(error) ? "" : Environment.NewLine + error));
        }

        percent?.Report(1);
    }


    /// <summary>
    /// 从 7z 的进度输出里取最后一个百分比。7z 用 <c>\b</c> 覆盖上一行，
    /// 因此一次读到的是多段 "  12%" 拼接，取最后一个即当前进度。
    /// </summary>
    internal static double? ParseLastPercent(ReadOnlySpan<char> text)
    {
        double? result = null;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '%')
            {
                continue;
            }

            var end = i;
            var start = end;
            while (start > 0 && char.IsAsciiDigit(text[start - 1]))
            {
                start--;
            }

            if (start == end)
            {
                continue;
            }

            if (int.TryParse(text[start..end], out var value))
            {
                result = value / 100d;
            }
        }

        return result;
    }


    private async Task ExtractZipFallbackAsync(IReadOnlyList<string> archives, string destination, IProgress<double>? percent, CancellationToken cancellationToken)
    {
        if (archives.Count == 1 && archives[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("7z not found, falling back to built-in zip extraction for {archive}", archives[0]);
            await Task.Run(() => ZipFile.ExtractToDirectory(archives[0], destination, overwriteFiles: true), cancellationToken);
            percent?.Report(1);
            return;
        }

        throw new InstallerCapabilityException(
            $"Cannot extract '{Path.GetFileName(archives[0])}': no 7z executable found in PATH " +
            $"(set {ExecutableEnvironmentVariable}, or install p7zip / 7zz). " +
            "Only plain .zip archives can be extracted without it.");
    }


    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // 已退出 / 无权限
        }
    }


    private static string? FindExecutable()
    {
        var configured = Environment.GetEnvironmentVariable(ExecutableEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in CandidateNames)
            {
                try
                {
                    var candidate = Path.Join(directory, name);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (Exception)
                {
                    // 目录含非法字符，跳过
                }
            }
        }

        return null;
    }
}
