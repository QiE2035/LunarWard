using LunarWard.Features.Database;
using LunarWard.Frameworks;
using LunarWard.Helpers;
using LunarWard.Setup.Core;
using Serilog;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace LunarWard;

public static partial class AppConfig
{

    public static string? StarwardPortableLauncherExecutePath { get; private set; }

    public static string AppVersion { get; private set; } = "";

    public static InstallType InstallType { get; set; }

    public static bool IsPortable => InstallType is InstallType.Portable;

    public static bool IsAppInRemovableStorage { get; private set; }

    public static string CacheFolder { get; private set; } = "";

    public static string ConfigPath { get; private set; } = "";

    public static string? Language { get; set; }

    public static string? UserDataFolder { get; set; }

    public static bool IsAdmin { get; private set; }

    public static string LogFile { get; private set; } = "";


    public static bool? EnableLoginAuthTicket { get; set; }

    public static string? stoken { get; set; }

    public static string? mid { get; set; }




    /// <summary>
    /// 读取 <see cref="AppVersion"/>（上游在 <c>CheckEnviroment</c> 开头做，等价于 WinUI 的包版本）。
    /// </summary>
    /// <remarks>
    /// 自检路径（<c>--screenshot</c>）会跳过 <see cref="CheckEnviromentAsync"/> 直接落默认数据目录，
    /// 但仍需要版本号构造 User-Agent，否则 <c>LunarWard.Debug/</c> 会被 <see cref="HttpHeaders"/> 判为非法。
    /// </remarks>
    internal static void InitializeVersion()
    {
        AppVersion = typeof(AppConfig).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
    }


    public static async Task CheckEnviromentAsync()
    {
        try
        {
            InitializeVersion();
            IsAppInRemovableStorage = DriveHelper.IsDeviceRemovableOrOnUSB(AppContext.BaseDirectory);

            string? parentFolder = new DirectoryInfo(AppContext.BaseDirectory).Parent?.FullName;
            string portableExe = Path.Join(parentFolder, "LunarWard");
            string portableVersion = Path.Join(parentFolder, "version.ini");

            if (Directory.Exists(parentFolder) && (File.Exists(portableExe) || File.Exists(portableVersion)))
            {
                InstallType = InstallType.Portable;
                StarwardPortableLauncherExecutePath = portableExe;
                if (!HaveWritePermission(parentFolder!))
                {
                    await StartupUi.ShowNoPermissionAsync(parentFolder!);
                    Environment.Exit(0);
                }
            }

            // 上游非 portable 时把配置写注册表；Linux 用 XDG 配置文件（docs/09）。
            string xdgConfigFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LunarWard");

            if (IsAppInRemovableStorage && IsPortable)
            {
                CacheFolder = Path.Combine(parentFolder!, ".cache");
                ConfigPath = Path.Combine(parentFolder!, "config.ini");
            }
            else if (IsAppInRemovableStorage)
            {
                CacheFolder = Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory)!, ".LunarWardCache");
                ConfigPath = Path.Combine(CacheFolder, "config.ini");
            }
            else if (IsPortable)
            {
                CacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunarWard");
                ConfigPath = Path.Combine(parentFolder!, "config.ini");
            }
            else
            {
                CacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LunarWard");
                ConfigPath = Path.Combine(xdgConfigFolder, "config.ini");
            }

            string? userDataFolder = null;
            if (!string.IsNullOrWhiteSpace(ConfigPath) && File.Exists(ConfigPath))
            {
                string text = File.ReadAllText(ConfigPath);
                userDataFolder = Regex.Match(text, @"UserDataFolder=(.+)").Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(userDataFolder) && !Path.IsPathFullyQualified(userDataFolder))
                {
                    userDataFolder = Path.GetFullPath(userDataFolder, Path.GetDirectoryName(ConfigPath)!);
                }
            }

            if (Directory.Exists(userDataFolder))
            {
                if (HaveWritePermission(userDataFolder!))
                {
                    UserDataFolder = userDataFolder;
                    DatabaseService.SetDatabase(userDataFolder!);
                    LoadConfiguration();
                }
                else
                {
                    await StartupUi.ShowNoPermissionAsync(userDataFolder!);
                    Environment.Exit(0);
                }
            }
            else
            {
                if (await StartupUi.ShowWelcomeAsync())
                {
                    LoadConfiguration();
                }
                else
                {
                    Environment.Exit(0);
                }
            }
        }
        catch (Exception ex)
        {
            // 上游是 User32.MessageBox；Linux 上写日志并退出（docs/09）。
            Log.Error(ex, "An unknown issue occurred during initialization");
            Console.Error.WriteLine($"{Lang.AppConfig_AnUnknownIssueOccurredDuringInitialization}\n{ex.Message}");
            Environment.Exit(0);
        }
    }


    private static bool HaveWritePermission(string folder)
    {
        try
        {
            string random = Path.Combine(folder, Guid.CreateVersion7().ToString());
            File.WriteAllBytes(random, "Write permission test."u8);
            File.Delete(random);
            return true;
        }
        catch
        {
            return false;
        }
    }



    public static void SetLanguage(string? lang)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(lang))
            {
                var info = new CultureInfo(lang);
                CultureInfo.CurrentUICulture = info;
                CultureInfo.DefaultThreadCurrentUICulture = info;
                Language = lang;
            }
            else
            {
                CultureInfo.CurrentUICulture = CultureInfo.InstalledUICulture;
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InstalledUICulture;
                Language = null;
            }
        }
        catch { }
    }



    public static void LoadConfiguration()
    {
        try
        {
            Directory.CreateDirectory(CacheFolder);
            FileCache.Initialize(Path.Combine(CacheFolder, "cache"));

            // 上游设置 WEBVIEW2_USER_DATA_FOLDER；Linux 的 WebKitGTK 数据目录由 Frameworks/WebView 自行管理（docs/09）。

            IsAdmin = Environment.IsPrivilegedProcess;

            if (string.IsNullOrWhiteSpace(ConfigPath))
            {
                LoadConfigurationFromConfigFile(Path.Combine(CacheFolder, "config.ini"));
            }
            else
            {
                LoadConfigurationFromConfigFile(ConfigPath);
            }
        }
        catch { }
    }


    public static void LoadConfigurationFromConfigFile(string path)
    {
        if (File.Exists(path))
        {
            string text = File.ReadAllText(path);
            string lang = Regex.Match(text, @"Language=(.+)").Groups[1].Value.Trim();
            bool.TryParse(Regex.Match(text, @"EnableLoginAuthTicket=(.+)").Groups[1].Value.Trim(), out bool enabled);
            EnableLoginAuthTicket = enabled;
            stoken = Regex.Match(text, @"stoken=(.+)").Groups[1].Value.Trim();
            mid = Regex.Match(text, @"mid=(.+)").Groups[1].Value.Trim();
            SetLanguage(lang);
        }
    }


    public static void SaveConfiguration()
    {
        SaveConfigurationToConfigFile();
    }


    public static void SaveConfigurationToConfigFile()
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(UserDataFolder))
            {
                string dataFolder = UserDataFolder;
                string? parentFolder = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrWhiteSpace(parentFolder) && UserDataFolder.StartsWith(parentFolder))
                {
                    dataFolder = Path.GetRelativePath(parentFolder, UserDataFolder);
                }
                sb.AppendLine($"Language={Language}");
                sb.AppendLine($"UserDataFolder={dataFolder}");
            }
            else
            {
                sb.AppendLine($"Language={Language}");
                sb.AppendLine($"UserDataFolder=");
            }
            if (EnableLoginAuthTicket.HasValue)
            {
                sb.AppendLine($"{nameof(EnableLoginAuthTicket)}={EnableLoginAuthTicket}");
            }
            if (!string.IsNullOrWhiteSpace(stoken))
            {
                sb.AppendLine($"{nameof(stoken)}={stoken}");
            }
            if (!string.IsNullOrWhiteSpace(mid))
            {
                sb.AppendLine($"{nameof(mid)}={mid}");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, sb.ToString());
        }
        catch { }
    }





}
