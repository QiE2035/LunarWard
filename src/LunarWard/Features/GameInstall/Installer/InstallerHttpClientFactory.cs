using System.Net;
using System.Net.Http;
using Polly;
using Polly.Retry;

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
/// <para>下面四项 handler 配置是 docs/02-ARCHITECTURE.md §4.3 明确要求保留的，
/// 不是可选项。</para>
///
/// <para><b>重试策略</b>：对应上游 <c>Starward.RPC/Program.cs:132</c> 的
/// <c>HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i))</c>，
/// 挂在 <c>AddHttpClient&lt;HoYoPlayClient&gt;()</c> 上。上游用它处理
/// <b>HTTP 层</b>的瞬时错误（5xx / 408 / <see cref="HttpRequestException"/>），
/// 与 <c>GameInstallService</c> 里那条"下载失败整体重试"的 <c>ResiliencePipeline</c>
/// 是<b>两条不同的策略</b>，两者都要有。</para>
///
/// <para>注意 <c>HttpPolicyExtensions</c> 与 <c>PolicyHttpMessageHandler</c> 位于
/// <c>Microsoft.Extensions.Http.Polly</c> 包，本项目没有引用它。
/// 这里用 Polly 8.8 的 <c>ResiliencePipeline</c> 精确复现同一语义，不新增依赖。</para>
/// </summary>
public interface IInstallerHttpClientFactory
{
    HttpClient CreateClient();
}


/// <summary>基于 <see cref="SocketsHttpHandler"/> 的默认实现（单例，可安全共享）。</summary>
public sealed class InstallerHttpClientFactory : IInstallerHttpClientFactory
{
    /// <summary>瞬时错误的重试次数，与上游 <c>WaitAndRetryAsync(3, ...)</c> 一致。</summary>
    public const int RetryCount = 3;


    /// <summary>
    /// HTTP 瞬时错误重试管线。
    ///
    /// <para>逐项对齐 <c>HandleTransientHttpError()</c>：它在 Polly 7 里等价于</para>
    /// <code>
    /// .Handle&lt;HttpRequestException&gt;()
    /// .OrResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
    /// </code>
    /// <para>退避为 <c>i =&gt; TimeSpan.FromSeconds(i)</c>：第 1 次重试等 1s，第 2 次等 2s，第 3 次等 3s。</para>
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> TransientHttpRetryPipeline { get; } =
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = RetryCount,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(static response => (int)response.StatusCode >= 500
                                                     || response.StatusCode == HttpStatusCode.RequestTimeout),
                // 上游 i => TimeSpan.FromSeconds(i) 是从 1 开始的（Polly 7 的 attempt 从 1 计），
                // Polly 8 的 AttemptNumber 从 0 计，因此 +1。
                DelayGenerator = static args => new ValueTask<TimeSpan?>(
                    TimeSpan.FromSeconds(args.AttemptNumber + 1)),
                BackoffType = DelayBackoffType.Constant,
            })
            .Build();


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


    /// <summary>
    /// 共享 handler，供测试断言 handler 配置用。
    /// </summary>
    public static SocketsHttpHandler Handler => SharedHandler;


    public HttpClient CreateClient()
    {
        // 把重试接在 handler 之上：重试的是 HTTP 请求本身（含请求体），
        // 与上游 PolicyHttpMessageHandler 的位置一致。
        var retryHandler = new RetryMessageHandler(TransientHttpRetryPipeline)
        {
            InnerHandler = SharedHandler,
        };

        return new HttpClient(retryHandler, disposeHandler: false)
        {
            // §4.3-6：宁愿协商到更高版本的 HTTP，也不要降级
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            Timeout = TimeSpan.FromMinutes(30),
        };
    }


    /// <summary>
    /// 把 <see cref="DelegatingHandler"/> 形式的请求套进 Polly 的
    /// <see cref="ResiliencePipeline{T}"/>，等价于上游的 <c>PolicyHttpMessageHandler</c>。
    /// </summary>
    private sealed class RetryMessageHandler : DelegatingHandler
    {
        private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

        public RetryMessageHandler(ResiliencePipeline<HttpResponseMessage> pipeline)
        {
            _pipeline = pipeline;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // 重试需要重发同一个请求；HttpRequestMessage 不可重复发送，
            // 因此每次尝试都克隆一份（上游 PolicyHttpMessageHandler 内部也是这么做的）。
            var buffered = await HttpRequestMessageCloner.CaptureAsync(request, cancellationToken).ConfigureAwait(false);

            return await _pipeline.ExecuteAsync(
                async token =>
                {
                    using var attempt = buffered.CreateAttempt();
                    return await base.SendAsync(attempt, token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }
    }
}


/// <summary>
/// 把一个 <see cref="HttpRequestMessage"/> 的内容缓冲起来，以便重试时重发。
/// </summary>
internal static class HttpRequestMessageCloner
{
    public static async Task<CapturedRequest> CaptureAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        byte[]? content = null;
        if (request.Content is not null)
        {
            content = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        var headers = request.Headers
            .Select(pair => new KeyValuePair<string, IEnumerable<string>>(pair.Key, pair.Value.ToArray()))
            .ToArray();

        var contentHeaders = request.Content?.Headers
            .Select(pair => new KeyValuePair<string, IEnumerable<string>>(pair.Key, pair.Value.ToArray()))
            .ToArray();

        return new CapturedRequest(request.Method, request.RequestUri, request.Version, request.VersionPolicy,
                                   headers, content, contentHeaders);
    }


    internal readonly record struct CapturedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        Version Version,
        HttpVersionPolicy VersionPolicy,
        KeyValuePair<string, IEnumerable<string>>[] Headers,
        byte[]? Content,
        KeyValuePair<string, IEnumerable<string>>[]? ContentHeaders)
    {
        /// <summary>新建一次尝试用的请求。内容已缓冲为数组，每次都是独立的 <see cref="ByteArrayContent"/>。</summary>
        public HttpRequestMessage CreateAttempt()
        {
            var message = new HttpRequestMessage(Method, RequestUri)
            {
                Version = Version,
                VersionPolicy = VersionPolicy,
            };

            if (Content is not null)
            {
                message.Content = new ByteArrayContent(Content);
                if (ContentHeaders is not null)
                {
                    foreach (var (name, values) in ContentHeaders)
                    {
                        message.Content.Headers.TryAddWithoutValidation(name, values);
                    }
                }
            }

            foreach (var (name, values) in Headers)
            {
                message.Headers.TryAddWithoutValidation(name, values);
            }

            return message;
        }
    }
}
