using System.Diagnostics;
using System.Text;

namespace LunarWard.IntegrationTests;

/// <summary>
/// 编译并缓存 <c>fakeproc.c</c>。它同时扮演"游戏"与"winedevice.exe"，
/// 用真实 <c>/proc</c> 而不是 mock —— 这样 <see cref="Platform.LinuxProcessTree"/>
/// 的实现本身也被测到了。
/// </summary>
public static class NativeHelper
{
    private static readonly Lazy<string?> Binary = new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<string?> DeviceBinary = new(BuildDevice, LazyThreadSafetyMode.ExecutionAndPublication);


    /// <summary>合成"游戏"进程的二进制路径；编译失败返回 <c>null</c>。</summary>
    public static string? Fakeproc => Binary.Value;

    /// <summary>名字为 <c>winedevice.exe</c> 的同一二进制（用于 AwaitReady 的 WineDevice 信号）。</summary>
    public static string? WineDevice => DeviceBinary.Value;


    public static bool Available => Fakeproc is not null;


    private static string SourcePath
    {
        get
        {
            // 从测试程序集所在目录往上找 native/fakeproc.c
            var dir = AppContext.BaseDirectory;
            for (var i = 0; i < 8 && dir is not null; i++)
            {
                var candidate = Path.Join(dir, "native", "fakeproc.c");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                var parent = Path.GetDirectoryName(dir);
                if (parent is null)
                {
                    break;
                }

                dir = parent;
            }

            return Path.Join(AppContext.BaseDirectory, "native", "fakeproc.c");
        }
    }


    private static string? Build() => Compile(SourcePath, "fakeproc");


    private static string? BuildDevice()
    {
        var binary = Binary.Value;
        if (binary is null)
        {
            return null;
        }

        // 名字决定 /proc/<pid>/comm，必须叫 winedevice.exe
        var dir = Path.GetDirectoryName(binary)!;
        var target = Path.Join(dir, "winedevice.exe");
        if (File.Exists(target))
        {
            return target;
        }

        File.Copy(binary, target);
        File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                     | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                                     | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        return target;
    }


    private static string? Compile(string source, string outputName)
    {
        if (!File.Exists(source))
        {
            return null;
        }

        var cc = Which("cc") ?? Which("gcc") ?? Which("clang");
        if (cc is null)
        {
            return null;
        }

        var cacheDir = Path.Join(Path.GetTempPath(), "lunarward-it-native");
        Directory.CreateDirectory(cacheDir);
        var output = Path.Join(cacheDir, outputName);

        // 已编译且比源文件新 → 直接复用
        if (File.Exists(output) && File.GetLastWriteTimeUtc(output) >= File.GetLastWriteTimeUtc(source))
        {
            return output;
        }

        var psi = new ProcessStartInfo
        {
            FileName = cc,
            ArgumentList = { "-O1", "-o", output, source },
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        using var process = Process.Start(psi);
        if (process is null)
        {
            return null;
        }

        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(TimeSpan.FromSeconds(60));
        if (process.ExitCode != 0 || !File.Exists(output))
        {
            Console.Error.WriteLine($"[fakeproc] cc failed: {stderr}");
            return null;
        }

        return output;
    }


    private static string? Which(string name)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Join(dir, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}


/// <summary>
/// 生成一个"假 wine"脚本。它模拟真实 wine 的两个关键副作用：
/// 进程继承 <c>WINEPREFIX</c>、把游戏 exe mmap 进 maps。
/// </summary>
public sealed class FakeWine
{
    private readonly StringBuilder _script = new();


    public FakeWine(string fakeprocBinary)
    {
        Fakeproc = fakeprocBinary;
        _script.AppendLine("#!/bin/bash");
        _script.AppendLine("set -u");
        _script.AppendLine("# fake wine: $1 = exe，其余为游戏参数");
        _script.AppendLine("EXE=\"${1:-}\"");
        _script.AppendLine("shift || true");
        _script.AppendLine("ARGS=\"$*\"");
        _script.AppendLine("echo \"fake-wine start EXE=$EXE ARGS=$ARGS\"");
        _script.AppendLine("echo \"WINEPREFIX=$WINEPREFIX\"");
    }


    public string Fakeproc { get; }

    /// <summary>游戏镜像 mmap 多久（秒）。</summary>
    public int GameSeconds { get; set; } = 2;

    /// <summary>退出码。</summary>
    public int ExitCode { get; set; }

    /// <summary>在启动游戏之前，先插入一个"非法的既有客户端"（验证 Prepare 清理）。</summary>
    public bool SpawnStaleClientBefore { get; set; }

    /// <summary>在启动游戏之后，额外 spawn 一个 <c>winedevice.exe</c>（验证 WineDevice 信号）。</summary>
    public bool SpawnWineDevice { get; set; }

    /// <summary>winedevice 延迟多少毫秒出现。</summary>
    public int WineDeviceDelayMs { get; set; } = 200;

    /// <summary>额外插入脚本里的行（测试用来模拟 Unity touch Player.log 等副作用）。</summary>
    public List<string> ExtraLines { get; } = [];


    /// <summary>返回给 <c>wine --version</c> 的字符串。</summary>
    public string Version { get; set; } = "wine-10.0 (fake)";


    public string Build(string targetPath, string deviceBinary)
    {
        foreach (var line in ExtraLines)
        {
            _script.AppendLine(line);
        }

        if (SpawnStaleClientBefore)
        {
            _script.AppendLine("\"$LUNARWARD_FAKEPROC\" - 600 &");
            _script.AppendLine("STALE=$!");
            _script.AppendLine("sleep 0.4");
            _script.AppendLine("echo \"stale client $STALE started\"");
        }

        _script.AppendLine("if [ \"${EXE:-}\" = \"--version\" ]; then");
        _script.AppendLine($"  echo \"{Version}\"");
        _script.AppendLine("  exit 0");
        _script.AppendLine("fi");

        _script.AppendLine("\"$LUNARWARD_FAKEPROC\" \"$PWD/$EXE\" " + GameSeconds + " &");
        _script.AppendLine("GAME=$!");

        if (SpawnWineDevice)
        {
            _script.AppendLine($"sleep {(WineDeviceDelayMs / 1000.0).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            _script.AppendLine($"\"{deviceBinary}\" - 5 &");
            _script.AppendLine("echo \"winedevice spawned\"");
        }

        _script.AppendLine("wait $GAME");
        _script.AppendLine($"exit {ExitCode}");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetPath))!);
        File.WriteAllText(targetPath, _script.ToString());
        File.SetUnixFileMode(targetPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        return targetPath;
    }
}
