using System.Text;
using System.Text.RegularExpressions;

namespace LunarWard.Wine;

/// <summary>
/// DXVK 安装与状态检测。
/// </summary>
/// <remarks>
/// 见 <c>docs/04-FEATURES.md</c> §3.7。an-anime-team <b>从不</b>使用
/// <c>WINEDLLOVERRIDES</c>，全靠注册表；本项目补上环境变量支持，但默认仍用注册表。
/// </remarks>
public interface IDxvkManager
{
    /// <summary>涉及的 DLL 名称。</summary>
    IReadOnlyList<string> DllNames { get; }

    /// <summary>检测 prefix 是否已装 DXVK（读 <c>user.reg</c>）。</summary>
    bool IsInstalled(string prefix);

    /// <summary>安装 DXVK 到 prefix。</summary>
    Task InstallAsync(string prefix, string dxvkDirectory, CancellationToken ct = default);

    /// <summary>卸载（把 <c>.old</c> 换回来、清注册表 override）。</summary>
    Task UninstallAsync(string prefix, CancellationToken ct = default);
}


/// <summary>默认实现。</summary>
public sealed class DxvkManager : IDxvkManager
{
    private static readonly string[] Dlls = ["dxgi", "d3d9", "d3d10core", "d3d11"];


    public IReadOnlyList<string> DllNames => Dlls;


    public bool IsInstalled(string prefix)
    {
        var userReg = Path.Join(prefix, "user.reg");
        if (!File.Exists(userReg))
        {
            return false;
        }

        try
        {
            foreach (var line in File.ReadLines(userReg))
            {
                // 形如："dxgi" = "native,builtin"
                if (line.Contains("\"dxgi\"", StringComparison.Ordinal)
                    && line.Contains("native", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (IOException)
        {
            return false;
        }

        return false;
    }


    public async Task InstallAsync(string prefix, string dxvkDirectory, CancellationToken ct = default)
    {
        if (!Directory.Exists(prefix) || !File.Exists(Path.Join(prefix, "system.reg")))
        {
            throw new InvalidOperationException($"prefix 无效：{prefix}");
        }

        if (!Directory.Exists(dxvkDirectory))
        {
            throw new DirectoryNotFoundException($"DXVK 目录不存在：{dxvkDirectory}");
        }

        var system32 = Path.Join(prefix, "drive_c", "windows", "system32");
        var syswow64 = Path.Join(prefix, "drive_c", "windows", "syswow64");
        Directory.CreateDirectory(system32);

        foreach (var dll in Dlls)
        {
            ct.ThrowIfCancellationRequested();
            var source = FindDll(dxvkDirectory, "x64", dll + ".dll");
            if (source is null)
            {
                continue;
            }

            var target = Path.Join(system32, dll + ".dll");
            if (File.Exists(target) && !File.Exists(target + ".old"))
            {
                File.Copy(target, target + ".old", overwrite: false);
            }

            File.Copy(source, target, overwrite: true);

            // 32 位 DLL（若组件包含）
            var source32 = FindDll(dxvkDirectory, "x32", dll + ".dll");
            if (source32 is not null && Directory.Exists(syswow64))
            {
                var target32 = Path.Join(syswow64, dll + ".dll");
                if (File.Exists(target32) && !File.Exists(target32 + ".old"))
                {
                    File.Copy(target32, target32 + ".old", overwrite: false);
                }

                File.Copy(source32, target32, overwrite: true);
            }
        }

        WriteDllOverrides(prefix, "native,builtin");
    }


    public Task UninstallAsync(string prefix, CancellationToken ct = default)
    {
        var system32 = Path.Join(prefix, "drive_c", "windows", "system32");
        var syswow64 = Path.Join(prefix, "drive_c", "windows", "syswow64");
        foreach (var dll in Dlls)
        {
            foreach (var dir in new[] { system32, syswow64 })
            {
                var target = Path.Join(dir, dll + ".dll");
                var backup = target + ".old";
                if (File.Exists(backup))
                {
                    File.Move(backup, target, overwrite: true);
                }
            }
        }

        WriteDllOverrides(prefix, null);
        return Task.CompletedTask;
    }


    private static string? FindDll(string root, string arch, string fileName)
    {
        // 组件目录结构有两种：<root>/x64/*.dll 与 <root>/*.dll
        var candidates = new[]
        {
            Path.Join(root, arch, fileName),
            Path.Join(root, "x64", fileName),
            Path.Join(root, fileName),
        };
        return candidates.FirstOrDefault(File.Exists);
    }


    /// <summary>
    /// 写 <c>HKCU\Software\Wine\DllOverrides</c>。直接改 <c>user.reg</c> 文本，
    /// 不依赖 <c>wine reg add</c>（避免多起一个 wine 客户端破坏不变式）。
    /// </summary>
    private static void WriteDllOverrides(string prefix, string? value)
    {
        var userReg = Path.Join(prefix, "user.reg");
        if (!File.Exists(userReg))
        {
            return;
        }

        var content = File.ReadAllText(userReg, Encoding.UTF8);
        var sectionHeader = "[Software\\\\Wine\\\\DllOverrides]";
        var idx = content.IndexOf(sectionHeader, StringComparison.Ordinal);

        if (value is null)
        {
            // 卸载：删掉这四行
            if (idx < 0)
            {
                return;
            }

            var end = content.IndexOf("\n[", idx + sectionHeader.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                end = content.Length;
            }

            var block = content[idx..end];
            var lines = block.Split('\n')
                .Where(l => !Dlls.Any(d => l.Contains($"\"{d}\"", StringComparison.Ordinal)))
                .ToArray();
            var updated = content[..idx] + string.Join('\n', lines) + content[end..];
            Platform.AtomicFile.WriteAllText(userReg, updated);
            return;
        }

        var sb = new StringBuilder();
        if (idx < 0)
        {
            // 追加一个新段落
            sb.Append(content.TrimEnd('\n'));
            sb.Append('\n');
            sb.Append(sectionHeader);
            sb.Append('\n');
            foreach (var dll in Dlls)
            {
                sb.Append($"\"{dll}\"=\"{value}\"\n");
            }
        }
        else
        {
            var end = content.IndexOf("\n[", idx + sectionHeader.Length, StringComparison.Ordinal);
            var before = end < 0 ? content : content[..end];
            var after = end < 0 ? "" : content[end..];

            // 先移除已有的同名键，再统一追加
            var lines = before.Split('\n')
                .Where(l => !Dlls.Any(d => l.StartsWith($"\"{d}\"", StringComparison.Ordinal)))
                .ToList();
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }

            sb.Append(string.Join('\n', lines));
            sb.Append('\n');
            foreach (var dll in Dlls)
            {
                sb.Append($"\"{dll}\"=\"{value}\"\n");
            }

            sb.Append(after);
        }

        Platform.AtomicFile.WriteAllText(userReg, sb.ToString());
    }
}
