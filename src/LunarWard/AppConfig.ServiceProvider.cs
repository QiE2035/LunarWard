using LunarWard.Core.Gacha.Genshin;
using LunarWard.Core.Gacha.StarRail;
using LunarWard.Core.Gacha.ZZZ;
using LunarWard.Core.GameNotice;
using LunarWard.Core.GameRecord;
using LunarWard.Core.HoYoPlay;
using LunarWard.Core.SelfQuery;
using LunarWard.Features.Database;
using LunarWard.Features.Gacha;
using LunarWard.Features.GameAccount;
using LunarWard.Features.GameLauncher;
using LunarWard.Features.HoYoPlay;
using LunarWard.Setup.Core;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Net;
using System.Net.Http;

namespace LunarWard;

public static partial class AppConfig
{

    private static IServiceProvider? _serviceProvider;


    private static void BuildServiceProvider()
    {
        if (_serviceProvider == null)
        {
            var logFolder = Path.Combine(CacheFolder, "log");
            Directory.CreateDirectory(logFolder);
            LogFile = Path.Combine(logFolder, $"LunarWard_{DateTime.Now:yyMMdd}.log");
            Log.Logger = new LoggerConfiguration().WriteTo.File(path: LogFile, shared: true, outputTemplate: $$"""[{Timestamp:HH:mm:ss.fff}] [{Level:u4}] [{{Path.GetFileName(Environment.ProcessPath)}} ({{Environment.ProcessId}})] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}""")
                                                  .Enrich.FromLogContext()
                                                  .CreateLogger();
            Log.Information($"Welcome to LunarWard v{AppVersion}\r\nSystem: {Environment.OSVersion}\r\nCommand Line: {Environment.CommandLine}");

            var sc = new ServiceCollection();
            sc.AddMemoryCache();
            sc.AddLogging(c => c.AddSerilog(Log.Logger));
            sc.AddHttpClient().ConfigureHttpClientDefaults(ConfigDefaultHttpClient);

            // 上游注册顺序与清单（AppConfig.ServiceProvider.cs）：逐个 Feature 组落地后取消注释。
            sc.AddSingleton<HoYoPlayClient>();
            sc.AddSingleton<GameNoticeClient>();
            sc.AddSingleton<HoYoPlayService>();
            // TODO(port): BackgroundService（Features/Background 落地后）
            sc.AddSingleton<GameLauncherService>();
            // TODO(port): GamePackageService（Features/GameInstall 落地后）
            // TODO(port): PlayTimeRecordService / PlayTimeStatsService（Features/PlayTime 落地后）
            sc.AddSingleton<GameNoticeService>();
            // TODO(port): SetupService

            sc.AddSingleton<GenshinGachaClient>();
            sc.AddSingleton<StarRailGachaClient>();
            sc.AddSingleton<ZZZGachaClient>();
            sc.AddSingleton<GenshinGachaService>();
            sc.AddSingleton<StarRailGachaService>();
            // TODO(port): ZZZGachaService / UIGFGachaService / GenshinBeyondGachaClient / GenshinBeyondGachaService

            sc.AddSingleton<HoyolabClient>();
            sc.AddSingleton<HyperionClient>();
            // TODO(port): GameRecordService（Features/GameRecord 落地后）

            sc.AddSingleton<SelfQueryClient>();
            // TODO(port): SelfQueryService

            sc.AddHttpClient<ReleaseClient>().ConfigLunarWardHttpClient();
            // TODO(port): UpdateService

            // TODO(port): RpcService / GameInstallService

            sc.AddSingleton<GameAuthLoginService>();
            sc.AddSingleton<GameAccountService>();

            // TODO(port): ScreenCaptureService

            // TODO(port): sc.AddHttpClient<LogUploadClient>().ConfigLunarWardHttpClient();


            _serviceProvider = sc.BuildServiceProvider();
        }
    }

    public static T GetService<T>() where T : notnull
    {
        BuildServiceProvider();
        return _serviceProvider!.GetRequiredService<T>();
    }

    public static ILogger<T> GetLogger<T>()
    {
        BuildServiceProvider();
        return _serviceProvider!.GetRequiredService<ILogger<T>>();
    }

    public static SqliteConnection CreateDatabaseConnection()
    {
        return DatabaseService.CreateConnection();
    }


    private static void ConfigDefaultHttpClient(this IHttpClientBuilder builder)
    {
        builder.RemoveAllLoggers();
        builder.ConfigureHttpClient(client =>
        {
            client.DefaultRequestHeaders.Clear();
#if DEBUG
            client.DefaultRequestHeaders.Add("User-Agent", $"LunarWard.Debug/{AppVersion}");
#else
            client.DefaultRequestHeaders.Add("User-Agent", $"LunarWard/{AppVersion}");
#endif
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        });
        builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true,
            EnableMultipleHttp3Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        });
    }


    private static void ConfigLunarWardHttpClient(this IHttpClientBuilder builder)
    {
        builder.RemoveAllLoggers();
        builder.ConfigureHttpClient(client =>
        {
            client.DefaultRequestHeaders.Clear();
#if DEBUG
            client.DefaultRequestHeaders.Add("User-Agent", $"LunarWard.Debug/{AppVersion}");
#else
            client.DefaultRequestHeaders.Add("User-Agent", $"LunarWard/{AppVersion}");
#endif
            // 以下三个头是上游服务端的线上协议（X-Sw-*），不能改名。
            client.DefaultRequestHeaders.Add("X-Sw-Device-Id", DeviceId.ToString());
            client.DefaultRequestHeaders.Add("X-Sw-Session-Id", SessionId.ToString());
            client.DefaultRequestHeaders.Add("X-Sw-App-Version", AppVersion);
            client.DefaultRequestHeaders.Add("X-Sw-App-Type", "Desktop");
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        });
        builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true,
            EnableMultipleHttp3Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        });
    }


}
