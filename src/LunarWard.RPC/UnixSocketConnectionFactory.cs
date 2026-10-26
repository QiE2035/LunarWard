using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LunarWard.RPC;

/// <summary>
/// 平台替换：NamedPipe → Unix socket。
/// 上游 NamedPipesConnectionFactory 用 NamedPipeClientStream 连接命名管道；
/// 这里用 <see cref="Socket"/> 连接同一路径的 Unix domain socket。
/// </summary>
internal sealed class UnixSocketConnectionFactory
{

    private readonly string _socketPath;


    public UnixSocketConnectionFactory(string socketPath)
    {
        _socketPath = socketPath;
    }


    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext _1, CancellationToken cancellationToken = default)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

}
