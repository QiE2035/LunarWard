using System.Net.Sockets;
using Avalonia.Threading;

namespace LunarWard.Services;

/// <summary>
/// 单实例守护：第二次启动不再开新窗口，而是把消息发给已在运行的实例。
/// </summary>
/// <remarks>
/// 上游用 Windows <c>AppInstance</c>（注册表 + 激活重定向）；Linux 上等价物是
/// 「Unix domain socket + 本地回环」：第一个实例在
/// <c>$TMPDIR/lunarward-{uid}.sock</c> 上监听，后来者连上去发一行 <c>activate</c> 后退出。
/// 这样托盘隐藏后，用户再点一次图标/桌面项也能把窗口带回来。
/// </remarks>
public static class SingleInstance
{
    private static Socket? _listener;

    private static string SocketPath =>
        Path.Join(Path.GetTempPath(), $"lunarward-{Environment.UserName}.sock");


    /// <summary>若已有实例在运行，通知它显示窗口并返回 true（调用方应随即退出）。</summary>
    public static bool TryActivateExisting()
    {
        try
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            client.Connect(new UnixDomainSocketEndPoint(SocketPath));
            client.Send("activate\n"u8);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }


    /// <summary>
    /// 成为主实例：清理上一个实例遗留的 socket 文件，然后监听。
    /// <paramref name="activate"/> 在 UI 线程上被调用（把窗口显示出来）。
    /// </summary>
    public static void Listen(Action activate)
    {
        try
        {
            File.Delete(SocketPath);
            _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
            _listener.Listen(4);

            _ = Task.Run(async () =>
            {
                bool warned = false;
                while (_listener is { } listener)
                {
                    try
                    {
                        using Socket connection = await listener.AcceptAsync().ConfigureAwait(false);
                        var buffer = new byte[64];
                        int read = await connection.ReceiveAsync(buffer).ConfigureAwait(false);
                        if (read > 0)
                        {
                            Dispatcher.UIThread.Post(activate);
                        }
                    }
                    catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException)
                    {
                        // 审计 M-22：一次异常连接曾让整个监听循环退出，之后再也唤不回窗口。
                        if (_listener is null)
                        {
                            break; // Dispose 主动关掉监听，正常退出
                        }
                        if (!warned)
                        {
                            warned = true;
                            Console.Error.WriteLine($"SINGLE_INSTANCE_ACCEPT_FAILED {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            // 单实例只是便利功能，失败不影响主窗口
            Console.Error.WriteLine($"SINGLE_INSTANCE_DISABLED {ex.GetType().Name}: {ex.Message}");
        }
    }


    public static void Dispose()
    {
        _listener?.Dispose();
        _listener = null;
        try
        {
            File.Delete(SocketPath);
        }
        catch (IOException)
        {
        }
    }
}
