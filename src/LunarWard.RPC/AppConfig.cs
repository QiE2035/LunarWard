using LunarWard.RPC.GameInstall;
using System;
using System.IO;
using System.Reflection;

namespace LunarWard.RPC;

internal static class AppConfig
{

    public static string AppVersion { get; } = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    public static string StartupMagic { get; } = "zb8L3ShgFjeyDxeA";

    public static string StarwardLauncherExecutePath { get; private set; } = "";

    public static bool IsPortable { get; private set; }

    public static bool IsAppInRemovableStorage { get; private set; }

    public static string CacheFolder { get; private set; } = "";


    /// <summary>
    /// 平台替换：NamedPipe → Unix socket。
    /// 目录：$XDG_RUNTIME_DIR/lunarward/，回退 /tmp/lunarward-$UID/。
    /// </summary>
    public static string SocketDirectory { get; }

    /// <summary>
    /// 平台替换：NamedPipe → Unix socket。服务端监听和客户端连接使用同一个 socket 路径。
    /// </summary>
    public static string SocketPath { get; }

    /// <summary>
    /// 平台替换：NamedPipe → Unix socket。用一个独占打开的锁文件实现上游命名 Mutex 的单实例语义。
    /// </summary>
    public static string InstanceLockPath { get; }


    static AppConfig()
    {
        IsAppInRemovableStorage = DriveHelper.IsDeviceRemovableOrOnUSB(AppContext.BaseDirectory);
        string? parentFolder = Directory.GetParent(AppContext.BaseDirectory)?.FullName;
        string launcherExePath = Path.Join(parentFolder, "LunarWard");
        if (Directory.Exists(parentFolder) && File.Exists(launcherExePath))
        {
            IsPortable = true;
            StarwardLauncherExecutePath = launcherExePath;
            CacheFolder = Path.Join(parentFolder, ".cache");
        }
        else
        {
            if (IsAppInRemovableStorage)
            {
                string? rootFolder = Path.GetPathRoot(AppContext.BaseDirectory);
                if (Directory.Exists(rootFolder))
                {
                    CacheFolder = Path.Join(rootFolder, ".LunarWardCache");
                }
            }
            CacheFolder = string.IsNullOrEmpty(CacheFolder) ? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunarWard") : CacheFolder;
        }
        Directory.CreateDirectory(CacheFolder);

        string? runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDir) && Directory.Exists(runtimeDir))
        {
            SocketDirectory = Path.Join(runtimeDir, "lunarward");
        }
        else
        {
            SocketDirectory = Path.Join("/tmp", $"lunarward-{NativeMethods.GetUid()}");
        }
        Directory.CreateDirectory(SocketDirectory);
        TrySetUnixFileMode(SocketDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        SocketPath = Path.Join(SocketDirectory, "rpc.sock");
        InstanceLockPath = Path.Join(SocketDirectory, "rpc.lock"); // 平台替换：NamedPipe → Unix socket
    }


    private static void TrySetUnixFileMode(string path, UnixFileMode mode)
    {
        try
        {
            File.SetUnixFileMode(path, mode);
        }
        catch (Exception)
        {

        }
    }

}
