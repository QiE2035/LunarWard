using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using LunarWard.Platform;

namespace LunarWard.Services;

/// <summary>
/// 通过 <c>wine reg</c> 读写 prefix 里的注册表值。
/// </summary>
/// <remarks>
/// <para>
/// Starward 的 <c>Features/GameSetting/GameSettingService.cs</c> 直接用
/// <c>Microsoft.Win32.Registry</c> 读写 <c>HKCU\Software\miHoYo\&lt;游戏&gt;</c>。
/// Linux 上没有 Windows 注册表，但游戏的设置在 Wine prefix 的
/// <c>user.reg</c> 里，用同一个 prefix 的 wine 执行 <c>reg query/add</c> 即可等价读写。
/// </para>
/// <para>
/// 只支持本页需要的两种类型：<c>REG_DWORD</c>（分辨率/全屏）与
/// <c>REG_BINARY</c>（原神的 <c>GENERAL_DATA</c> JSON，里面含语音语言）。
/// </para>
/// <para>
/// 所有调用都带超时并忽略 wine 的 stderr 噪音；失败返回 <c>null</c>/<c>false</c>，
/// 由调用方决定提示文案，不抛异常打断 UI。
/// </para>
/// </remarks>
public sealed class WineRegistry
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly IProcessLauncher _launcher;

    private readonly LogService _log;


    public WineRegistry(IProcessLauncher launcher, LogService log)
    {
        _launcher = launcher;
        _log = log;
    }


    /// <summary>读 <c>REG_DWORD</c>。值不存在/读失败返回 <c>null</c>。</summary>
    public async Task<int?> ReadDwordAsync(
        string wineBinary,
        string prefix,
        string keyPath,
        string valueName,
        CancellationToken ct = default)
    {
        var output = await QueryAsync(wineBinary, prefix, keyPath, valueName, ct).ConfigureAwait(false);
        return output is null ? null : ParseDword(output, valueName);
    }


    /// <summary>读 <c>REG_BINARY</c> 的原始字节。值不存在/读失败返回 <c>null</c>。</summary>
    public async Task<byte[]?> ReadBinaryAsync(
        string wineBinary,
        string prefix,
        string keyPath,
        string valueName,
        CancellationToken ct = default)
    {
        var output = await QueryAsync(wineBinary, prefix, keyPath, valueName, ct).ConfigureAwait(false);
        return output is null ? null : ParseBinary(output, valueName);
    }


    /// <summary>写 <c>REG_DWORD</c>。返回是否成功。</summary>
    public Task<bool> WriteDwordAsync(
        string wineBinary,
        string prefix,
        string keyPath,
        string valueName,
        int value,
        CancellationToken ct = default)
    {
        return AddAsync(
            wineBinary,
            prefix,
            [keyPath, "/v", valueName, "/t", "REG_DWORD", "/d", value.ToString(CultureInfo.InvariantCulture), "/f"],
            ct);
    }


    /// <summary>写 <c>REG_BINARY</c>（字节序列转十六进制）。返回是否成功。</summary>
    public Task<bool> WriteBinaryAsync(
        string wineBinary,
        string prefix,
        string keyPath,
        string valueName,
        byte[] data,
        CancellationToken ct = default)
    {
        return AddAsync(
            wineBinary,
            prefix,
            [keyPath, "/v", valueName, "/t", "REG_BINARY", "/d", Convert.ToHexString(data), "/f"],
            ct);
    }


    // ── reg query / add ───────────────────────────────────────────────

    private async Task<string?> QueryAsync(
        string wineBinary,
        string prefix,
        string keyPath,
        string valueName,
        CancellationToken ct)
    {
        var (exitCode, output, error) = await RunAsync(
            wineBinary,
            prefix,
            ["reg", "query", keyPath, "/v", valueName],
            ct).ConfigureAwait(false);

        if (exitCode != 0)
        {
            // 键或值不存在是正常情况（游戏还没写过设置），不记错误。
            _log.Info($"wine reg query 未命中（{valueName}, exit {exitCode}）：{Trim(error)}");
            return null;
        }

        return output;
    }


    private async Task<bool> AddAsync(
        string wineBinary,
        string prefix,
        IReadOnlyList<string> arguments,
        CancellationToken ct)
    {
        var (exitCode, _, error) = await RunAsync(
            wineBinary,
            prefix,
            ["reg", "add", .. arguments],
            ct).ConfigureAwait(false);

        if (exitCode != 0)
        {
            _log.Warn($"wine reg add 失败（{string.Join(' ', arguments)}, exit {exitCode}）：{Trim(error)}");
            return false;
        }

        return true;
    }


    private async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string wineBinary,
        string prefix,
        IReadOnlyList<string> arguments,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(wineBinary) || !File.Exists(wineBinary))
        {
            return (-1, "", $"wine binary not found: {wineBinary}");
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return (-1, "", "empty WINEPREFIX");
        }

        var env = BuildEnvironment(prefix);
        var request = new ProcessRequest
        {
            FileName = wineBinary,
            Arguments = arguments,
            WorkingDirectory = prefix,
            Environment = env,
            RedirectOutput = true,
            RedirectError = true,
            // 注册表操作很短命，不需要独立会话；避免 setsid 包装影响退出码解析。
            NewSession = false,
        };

        using var process = _launcher.Start(request);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);

        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            return (process.ExitCode, await outputTask.ConfigureAwait(false), await errorTask.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return (-1, "", "wine reg timed out");
        }
        catch (Exception ex)
        {
            TryKill(process);
            return (-1, "", ex.Message);
        }
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
        catch
        {
            // 进程已退出/已回收
        }
    }


    private static Dictionary<string, string> BuildEnvironment(string prefix)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                env[key] = value;
            }
        }

        env["WINEPREFIX"] = prefix;
        env["WINEDEBUG"] = "-all";
        // 与 PrefixManager 一致：别让 wine 停下来问 mono/gecko。
        env["WINEDLLOVERRIDES"] = MergeDllOverrides(env.GetValueOrDefault("WINEDLLOVERRIDES"));
        return env;
    }


    private static string MergeDllOverrides(string? current)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(current))
        {
            parts.AddRange(current.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        foreach (var item in new[] { "mscoree=", "mshtml=" })
        {
            if (!parts.Contains(item, StringComparer.OrdinalIgnoreCase))
            {
                parts.Add(item);
            }
        }

        return string.Join(';', parts);
    }


    private static string Trim(string text)
        => text.Length <= 300 ? text.Trim() : text[..300].Trim() + "…";


    // ── 输出解析（纯函数，单测直接调） ────────────────────────────────

    /// <summary>
    /// 从 <c>reg query</c> 输出里取 <c>REG_DWORD</c>。
    /// 形如：<c>    Screenmanager_Resolution_Width_h182942802    REG_DWORD    0x780</c>
    /// </summary>
    public static int? ParseDword(string output, string valueName)
    {
        foreach (var line in output.Split('\n'))
        {
            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3)
            {
                continue;
            }

            if (!tokens[0].Equals(valueName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!tokens[1].Equals("REG_DWORD", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = tokens[2].Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
            {
                return hex;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec))
            {
                return dec;
            }
        }

        return null;
    }


    /// <summary>
    /// 从 <c>reg query</c> 输出里取 <c>REG_BINARY</c> 的十六进制串。
    /// 形如：<c>    GENERAL_DATA_h2389025596    REG_BINARY    7B2264...</c>
    /// </summary>
    public static byte[]? ParseBinary(string output, string valueName)
    {
        foreach (var line in output.Split('\n'))
        {
            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3)
            {
                continue;
            }

            if (!tokens[0].Equals(valueName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!tokens[1].Equals("REG_BINARY", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hex = tokens[2].Trim();
            if (hex.Length % 2 != 0)
            {
                return null;
            }

            try
            {
                return Convert.FromHexString(hex);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return null;
    }


    /// <summary>二进制注册表值是 UTF-8 文本 + 结尾 <c>\0</c>（Starward 的写入口径）。</summary>
    public static string? DecodeUtf8Z(byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            return null;
        }

        return Encoding.UTF8.GetString(data).TrimEnd('\0');
    }


    /// <summary>把文本按 Starward 的口径编码成 <c>UTF-8 + \0</c>。</summary>
    public static byte[] EncodeUtf8Z(string text)
        => [.. Encoding.UTF8.GetBytes(text), 0];
}
