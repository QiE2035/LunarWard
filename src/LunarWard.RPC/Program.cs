using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using LunarWard.Core.HoYoPlay;
using LunarWard.RPC.Env;
using LunarWard.RPC.GameInstall;
using LunarWard.Setup.Core;
using System;
using System.IO;
using System.Net;
using System.Net.Http;


namespace LunarWard.RPC;

public static class RpcRunner
{


    public static void Run(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Warning";

        var logFolder = Path.Combine(AppConfig.CacheFolder, "log");
        Directory.CreateDirectory(logFolder);
        var logFile = Path.Combine(logFolder, $"LunarWard_{DateTime.Now:yyMMdd}.log");
        Log.Logger = new LoggerConfiguration().WriteTo.File(path: logFile, shared: true, outputTemplate: $$"""[{Timestamp:HH:mm:ss.fff}] [{Level:u4}] [LunarWard RPC ({{Environment.ProcessId}})] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}""")
                                          .Enrich.FromLogContext()
                                          .ReadFrom.Configuration(builder.Configuration)
                                          .CreateLogger();
        Log.Information($"Welcome to LunarWard RPC v{AppConfig.AppVersion}\r\nSystem: {Environment.OSVersion}\r\nCommand Line: {Environment.CommandLine}");

        if (args.Length < 2 || args[1] != AppConfig.StartupMagic)
        {
            Log.Warning("Start magic is wrong, exit process!");
            return;
        }

        // 平台替换：NamedPipe → Unix socket。上游用命名 Mutex 保证单实例，这里改用独占打开的锁文件。
        using FileStream instanceLock = new FileStream(AppConfig.InstanceLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        // 平台替换：NamedPipe → Unix socket。清理上次异常退出残留的 socket 文件。
        if (File.Exists(AppConfig.SocketPath))
        {
            File.Delete(AppConfig.SocketPath);
        }

        if (args.Length > 2 && int.TryParse(args[2], out int processId))
        {
            LifecycleManager.SetParentProcess(processId, false);
        }

        GCTimer.Start();


        // 平台替换：NamedPipe → Unix socket。.NET 10 上 Kestrel 直接监听 Unix domain socket。
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.ListenUnixSocket(AppConfig.SocketPath, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });


        builder.Services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaxReceiveMessageSize = 64 << 20;
        });

        builder.Services.AddHttpClient().ConfigureHttpClientDefaults(config =>
        {
            config.RemoveAllLoggers();
            config.ConfigureHttpClient(client =>
            {
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
                client.DefaultRequestHeaders.Add("User-Agent", $"LunarWard.RPC/{AppConfig.AppVersion}");
            });
            config.ConfigurePrimaryHttpMessageHandler(GetDefaultSocketsHttpHandler);
        });
        builder.Services.AddHttpClient<HoYoPlayClient>().AddPolicyHandler(GetHttpRetryPolicy());


        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);


        builder.Services.AddScoped<ReleaseClient>();
        //builder.Services.AddScoped<HoYoPlayClient>();
        builder.Services.AddScoped<GamePackageService>();
        builder.Services.AddSingleton<GameInstallService>();
        builder.Services.AddSingleton<GameInstallHelper>();
        builder.Services.AddScoped<GameUninstallService>();



        var app = builder.Build();


        app.MapGrpcService<EnviromentController>();
        app.MapGrpcService<GameInstallController>();


        app.Start();

        // 平台替换：NamedPipe → Unix socket。socket 文件本身收紧到 600。
        File.SetUnixFileMode(AppConfig.SocketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        // 平台替换自测：LUNARWARD_RPC_SELFTEST=1 时走同一个 socket 自己 ping 一次再退出，
        // 用于验证「服务端 Kestrel + 客户端 Unix ConnectCallback」这一对替换是活的。
        if (Environment.GetEnvironmentVariable("LUNARWARD_RPC_SELFTEST") is "1")
        {
            try
            {
                var client = RpcClientFactory.CreateRpcClient<global::LunarWard.RPC.Env.Env.EnvClient>();
                var info = client.GetRpcServerInfoAsync(new EmptyMessage(), deadline: DateTime.UtcNow.AddSeconds(10)).GetAwaiter().GetResult();
                Console.WriteLine($"SELFTEST_OK pid={info.ProcessId} path={info.ProcessPath}");
                Environment.ExitCode = info.ProcessId == Environment.ProcessId ? 0 : 3;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELFTEST_FAIL " + ex);
                Environment.ExitCode = 2;
            }
            app.StopAsync().GetAwaiter().GetResult();
            return;
        }

        app.WaitForShutdown();
    }


    private static IAsyncPolicy<HttpResponseMessage> GetHttpRetryPolicy()
    {
        return HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i));
    }


    private static SocketsHttpHandler GetDefaultSocketsHttpHandler()
    {
        return new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            EnableMultipleHttp2Connections = true,
            EnableMultipleHttp3Connections = true,
        };
    }


}
