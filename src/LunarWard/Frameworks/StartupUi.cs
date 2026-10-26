using LunarWard.Features.Database;
using Serilog;

namespace LunarWard.Frameworks;

/// <summary>
/// 上游在 AppConfig.CheckEnviromentAsync 里直接 new NoPermissionWindow / new WelcomeWindow。
/// Linux 上没有 WinUI，这两个窗口由 Features/ViewHost 后续移植；在那之前用本类的可插拔钩子承接，
/// 默认实现不会静默退出，而是写日志并落到一个可用的默认方案上（见 docs/09）。
/// </summary>
public static class StartupUi
{

    /// <summary>设置页/外壳就绪后由 ViewHost 注入 NoPermissionWindow 的等价实现。</summary>
    public static Func<string, Task>? NoPermissionHandler { get; set; }

    /// <summary>设置页/外壳就绪后由 ViewHost 注入 WelcomeWindow 的等价实现（true = 用户已完成目录选择）。</summary>
    public static Func<Task<bool>>? WelcomeHandler { get; set; }


    public static Task ShowNoPermissionAsync(string folder)
    {
        if (NoPermissionHandler is not null)
        {
            return NoPermissionHandler(folder);
        }
        Log.Error("No write permission for folder: {Folder}", folder);
        Console.Error.WriteLine($"No write permission for folder: {folder}");
        return Task.CompletedTask;
    }


    public static async Task<bool> ShowWelcomeAsync()
    {
        if (WelcomeHandler is not null)
        {
            return await WelcomeHandler();
        }
        // 默认：把用户数据目录落在 CacheFolder（~/.local/share/LunarWard），不阻塞首次启动。
        string folder = AppConfig.CacheFolder;
        Directory.CreateDirectory(folder);
        AppConfig.UserDataFolder = folder;
        DatabaseService.SetDatabase(folder);
        Log.Information("First launch without WelcomeWindow, using default user data folder: {Folder}", folder);
        return true;
    }

}
