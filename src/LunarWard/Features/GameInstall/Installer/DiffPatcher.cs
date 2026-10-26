using System.Diagnostics;

namespace LunarWard.Installer;

/// <summary>
/// hpatch 差量合并抽象。
/// </summary>
/// <remarks>
/// <para>
/// 上游用 <c>Snap.HPatch</c>（<c>HPatch.PatchZstandard</c>）。该包<b>只有
/// <c>runtimes/win-x64/native</c></b> 的二进制，Linux 上不可用（实测 nupkg 内容）。
/// </para>
/// <para>
/// 因此这里改为调用系统的 <c>hpatchz</c>（hdiffpatch 官方 CLI，Arch 在
/// <c>hdiffpatch</c> 包、Debian 在 <c>hdiffpatch</c>、也可由用户自备）。
/// 找不到 CLI 时：若 diff 与源文件不存在则跳过，若存在则抛
/// <see cref="InstallerCapabilityException"/> 并给出安装提示——绝不静默产出坏文件。
/// </para>
/// </remarks>
public interface IDiffPatcher
{
    /// <summary>hpatchz 可执行文件路径；<c>null</c> 表示不可用。</summary>
    string? Executable { get; }

    bool IsAvailable { get; }

    /// <summary>
    /// 应用 zstd 压缩的 hdiff 补丁。
    /// <paramref name="sourcePath"/> 为 <c>null</c> 表示"无源文件"（全量补丁）。
    /// </summary>
    /// <returns>成功返回 <c>true</c>。</returns>
    bool TryPatch(string? sourcePath, string diffPath, Stream target);
}


/// <summary>基于外部 <c>hpatchz</c> 的实现。</summary>
public sealed class HdiffPatchPatcher : IDiffPatcher
{
    /// <summary>允许通过环境变量指定 hpatchz 路径。</summary>
    public const string ExecutableEnvironmentVariable = "LUNARWARD_HPATCHZ";

    private static readonly string[] CandidateNames = ["hpatchz", "hpatch"];


    private readonly IInstallerLogger _logger;


    public HdiffPatchPatcher(IInstallerLogger? logger = null)
    {
        _logger = logger ?? NullInstallerLogger.Instance;
        Executable = FindExecutable();
    }


    public string? Executable { get; }


    public bool IsAvailable => Executable is not null;


    public bool TryPatch(string? sourcePath, string diffPath, Stream target)
    {
        if (Executable is null)
        {
            _logger.LogWarning(
                "hpatchz 不可用：无法应用差量补丁 {Diff}。请安装 hdiffpatch（Arch: pacman -S hdiffpatch；Debian: apt install hdiffpatch），"
                + "或设置环境变量 {Env} 指向 hpatchz。",
                diffPath, ExecutableEnvironmentVariable);
            return false;
        }

        if (!File.Exists(diffPath))
        {
            _logger.LogWarning("补丁文件不存在 {Diff}", diffPath);
            return false;
        }

        // hpatchz 需要输出到文件；写临时文件后再拷进 Stream
        var temp = Path.Combine(Path.GetTempPath(), "lunarward-hpatch-" + Guid.NewGuid().ToString("N")[..10]);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Executable,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            // hpatchz [-s] old diff new    /    hpatchz diff new  (无源)
            if (sourcePath is not null && File.Exists(sourcePath))
            {
                psi.ArgumentList.Add(sourcePath);
                psi.ArgumentList.Add(diffPath);
                psi.ArgumentList.Add(temp);
            }
            else
            {
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add(diffPath);
                psi.ArgumentList.Add(temp);
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(TimeSpan.FromMinutes(10));
            if (process.ExitCode != 0 || !File.Exists(temp))
            {
                _logger.LogWarning("hpatchz 失败（退出码 {Code}）：{Stderr}", process.ExitCode, stderr);
                return false;
            }

            using (var fs = File.OpenRead(temp))
            {
                fs.CopyTo(target);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调用 hpatchz 失败 {Diff}", diffPath);
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }
    }


    private static string? FindExecutable()
    {
        var explicitPath = Environment.GetEnvironmentVariable(ExecutableEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            return explicitPath;
        }

        foreach (var name in CandidateNames)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Join(dir, name);
                if (File.Exists(candidate) && (File.GetUnixFileMode(candidate) & UnixFileMode.UserExecute) != 0)
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
