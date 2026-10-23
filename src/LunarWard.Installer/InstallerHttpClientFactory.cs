using System.Net;
using System.Net.Http;

namespace LunarWard.Installer;

/// <summary>
/// 创建安装器用的 <see cref="HttpClient"/>。
///
/// <para>上游用 <c>IHttpClientFactory</c>（<c>Microsoft.Extensions.Http</c>）。
/// 本项目不引入该包，改为一个极小的工厂：所有客户端共享同一个
/// <see cref="SocketsHttpHandler"/>，每次 <see cref="CreateClient"/> 返回新的
/// <see cref="HttpClient"/> 包装它（<c>disposeHandler: false</c>），
/// 这样既能保持上游"每个下载任务一个新 client、用完即弃"的写法，
/// 又不会为每次请求重建连接池。</para>
///
/// <para>下面四项是 docs/02-ARCHITECTURE.md §4.3 明确要求保留的配置，不是可选项。</para>
/// </summary>
public interface IInstallerHttpClientFactory
{
    HttpClient CreateClient();
}


/// <summary>基于 <see cref="SocketsHttpHandler"/> 的默认实现（单例，可安全共享）。</summary>
public sealed class InstallerHttpClientFactory : IInstallerHttpClientFactory
{
    /// <summary>
    /// 进程级共享 handler。配置与上游 <c>Starward.RPC/Program.cs</c> 中的
    /// <c>ConfigurePrimaryHttpMessageHandler</c> 逐项一致。
    /// </summary>
    private static readonly SocketsHttpHandler SharedHandler = new()
    {
        // §4.3-3a：HoYoPlay CDN 对 zstd/br 有支持，全部声明可省下大量带宽
        AutomaticDecompression = DecompressionMethods.All,
        // §4.3-3b：CDN 会轮换后端，5 分钟换一次连接避免粘在慢节点上
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        // §4.3-3c：chunk 是海量小请求，单连接多路复用不够用
        EnableMultipleHttp2Connections = true,
        // §4.3-3d：同上，为将来 HTTP/3 铺路（对方不支持时自动降级）
        EnableMultipleHttp3Connections = true,
    };


    public static InstallerHttpClientFactory Instance { get; } = new();


    public HttpClient CreateClient()
    {
        return new HttpClient(SharedHandler, disposeHandler: false)
        {
            // §4.3-6：宁愿协商到更高版本的 HTTP，也不要降级
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            Timeout = TimeSpan.FromMinutes(30),
        };
    }
}
