using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace LunarWard.RPC;

public static class RpcClientFactory
{

    public static string StartupMagic => AppConfig.StartupMagic;


    public static bool CheckRpcServerRunning()
    {
        // 平台替换：NamedPipe → Unix socket。上游用命名 Mutex 判断服务端是否存在，
        // 这里尝试独占打开锁文件：能拿到说明服务端没在跑，独占失败说明服务端持有。
        if (!File.Exists(AppConfig.SocketPath))
        {
            return false;
        }
        try
        {
            using FileStream stream = new FileStream(AppConfig.InstanceLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }



    [Obsolete("Not used", true)]
    public static async Task<Process> EnsureRpcServerRunningAsync()
    {
        Process? process = null;
        if (!CheckRpcServerRunning())
        {
            // 平台替换：NamedPipe → Unix socket。上游用 runas 以管理员权限启动 Starward.RPC.exe；
            // Linux 没有 UAC 提权模型，直接启动同一个可执行文件的 rpc 子命令。
            string name = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "LunarWard");
            process = Process.Start(new ProcessStartInfo
            {
                FileName = name,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"rpc {AppConfig.StartupMagic} {Environment.ProcessId}",
            });
        }
        try
        {
            var client = new Env.Env.EnvClient(CreateChannel());
            var info = await client.GetRpcServerInfoAsync(new EmptyMessage(), deadline: DateTime.UtcNow.AddSeconds(5));
            return process ?? Process.GetProcessById(info.ProcessId);
        }
        catch (RpcException ex) when (ex.Status is { StatusCode: StatusCode.DeadlineExceeded })
        {
            throw new TimeoutException("Checking RPC server timed out.");
        }
    }




    private static GrpcChannel CreateChannel()
    {
        // 平台替换：NamedPipe → Unix socket。
        var connectionFactory = new UnixSocketConnectionFactory(AppConfig.SocketPath);
        var socketsHttpHandler = new SocketsHttpHandler
        {
            ConnectCallback = connectionFactory.ConnectAsync,
            // 防止因为系统代理无法建立连接
            UseProxy = false,
        };
        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = socketsHttpHandler
        });
    }




    public static T CreateRpcClient<T>() where T : ClientBase<T>
    {
        return (T)Activator.CreateInstance(typeof(T), CreateChannel())!;
    }



}
