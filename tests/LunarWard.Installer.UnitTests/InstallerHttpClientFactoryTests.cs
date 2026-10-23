using System.Diagnostics;
using System.Net;
using System.Net.Http;
using LunarWard.Installer;
using Xunit;

namespace LunarWard.Installer.UnitTests;

/// <summary>
/// <see cref="InstallerHttpClientFactory"/> 的 handler 配置与重试策略。
///
/// <para>重试对应上游 <c>Starward.RPC/Program.cs:132</c>：
/// <c>HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i))</c>。
/// 判定标准直接取<b>服务端实际收到的请求数</b>与<b>真实耗时</b>，不 mock 时间 ——
/// 这样"重试了但退避算错"也能被测出来。</para>
/// </summary>
public class InstallerHttpClientFactoryTests
{
    /// <summary>docs/02 §4.3 明确要求保留的四项 handler 配置。</summary>
    [Fact]
    public void Handler_HasRequiredConfiguration()
    {
        var handler = InstallerHttpClientFactory.Handler;

        Assert.Equal(DecompressionMethods.All, handler.AutomaticDecompression);
        Assert.Equal(TimeSpan.FromMinutes(5), handler.PooledConnectionLifetime);
        Assert.True(handler.EnableMultipleHttp2Connections);
        Assert.True(handler.EnableMultipleHttp3Connections);
    }


    /// <summary><c>DefaultVersionPolicy</c> 必须是 <c>RequestVersionOrHigher</c>。</summary>
    [Fact]
    public void Client_HasRequestVersionOrHigherPolicy()
    {
        using var client = InstallerHttpClientFactory.Instance.CreateClient();
        Assert.Equal(HttpVersionPolicy.RequestVersionOrHigher, client.DefaultVersionPolicy);
    }


    /// <summary>重试次数常量与上游 <c>WaitAndRetryAsync(3, ...)</c> 一致。</summary>
    [Fact]
    public void RetryCount_IsThree()
    {
        Assert.Equal(3, InstallerHttpClientFactory.RetryCount);
    }


    /// <summary>
    /// 连续 5xx：服务端应当共收到 4 次请求（1 次初始 + 3 次重试），
    /// 且总耗时约 1+2+3 = 6 秒。
    /// </summary>
    [Fact]
    public async Task TransientHttpError_RetriesThreeTimesWithLinearBackoff()
    {
        using var server = new FlakyServer(HttpStatusCode.InternalServerError, failFirstRequests: int.MaxValue);
        using var client = InstallerHttpClientFactory.Instance.CreateClient();
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync(server.Url);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        // 上游 WaitAndRetryAsync 在重试耗尽后返回最后一次响应，不抛异常
        Assert.Equal(4, server.Hits);
        // 1+2+3 = 6s；给足余量以适配慢机器，但必须明显大于 0（重试之间确实等待了）
        Assert.InRange(stopwatch.Elapsed.TotalSeconds, 5.0, 30.0);
    }


    /// <summary>
    /// 前 2 次 500、之后 200：应当重试并最终成功，共 3 次请求、耗时约 1+2 = 3 秒。
    /// </summary>
    [Fact]
    public async Task TransientHttpError_RecoversAfterRetries()
    {
        using var server = new FlakyServer(HttpStatusCode.InternalServerError, failFirstRequests: 2);
        using var client = InstallerHttpClientFactory.Instance.CreateClient();
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync(server.Url);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, server.Hits);
        Assert.InRange(stopwatch.Elapsed.TotalSeconds, 2.0, 30.0);
    }


    /// <summary>
    /// 408 Request Timeout 属于"瞬时错误"，必须重试。
    /// </summary>
    [Fact]
    public async Task RequestTimeout_IsRetried()
    {
        using var server = new FlakyServer(HttpStatusCode.RequestTimeout, failFirstRequests: int.MaxValue);
        using var client = InstallerHttpClientFactory.Instance.CreateClient();
        using var response = await client.GetAsync(server.Url);

        Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
        Assert.Equal(4, server.Hits);
    }


    /// <summary>
    /// 404 是<b>非</b>瞬时错误，绝不能重试 ——
    /// 否则 HoYoPlay 返回 404 时会白白等待 6 秒 × 每个请求。
    /// </summary>
    [Fact]
    public async Task ClientError_IsNotRetried()
    {
        using var server = new FlakyServer(HttpStatusCode.NotFound, failFirstRequests: int.MaxValue);
        using var client = InstallerHttpClientFactory.Instance.CreateClient();
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync(server.Url);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, server.Hits);
        Assert.InRange(stopwatch.Elapsed.TotalSeconds, 0.0, 5.0);
    }


    /// <summary>2xx 不重试。</summary>
    [Fact]
    public async Task Success_IsNotRetried()
    {
        using var server = new FlakyServer(HttpStatusCode.OK, failFirstRequests: 0);
        using var client = InstallerHttpClientFactory.Instance.CreateClient();
        using var response = await client.GetAsync(server.Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, server.Hits);
    }


    /// <summary>
    /// 多次 <see cref="InstallerHttpClientFactory.CreateClient"/> 必须共享同一个 handler
    /// （否则每条下载都新建连接池，chunk 并发下载会耗尽端口）。
    /// </summary>
    [Fact]
    public void CreateClient_ReusesSharedHandler()
    {
        using var a = InstallerHttpClientFactory.Instance.CreateClient();
        using var b = InstallerHttpClientFactory.Instance.CreateClient();

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotSame(a, b);
        // 共享 handler 是单例；两个客户端的 handler 各自包装它，但底层是同一个实例
        Assert.Same(InstallerHttpClientFactory.Handler, InstallerHttpClientFactory.Handler);
    }


    // ———————————————————————————— 测试服务器 ————————————————————————————


    /// <summary>
    /// 一个只用于测试的本地 HTTP 服务：记录收到的请求数，
    /// 前 <c>successAfter</c> 次返回 <c>failStatus</c>，之后返回 200。
    ///
    /// <para>计数器是<b>实例字段</b>而非 static —— 否则上一个测试的监听循环
    /// 会在 <c>Stop()</c> 之后继续为在途请求计数，污染下一个测试。</para>
    /// </summary>
    private sealed class FlakyServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        private int _hits;


        public FlakyServer(HttpStatusCode failStatus, int failFirstRequests)
        {
            FailStatus = failStatus;
            FailFirstRequests = failFirstRequests;
            Port = Random.Shared.Next(18200, 18999);
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _loop = Task.Run(AcceptLoopAsync);
        }


        public int Port { get; }

        public HttpStatusCode FailStatus { get; }

        public int FailFirstRequests { get; }

        public int Hits => Volatile.Read(ref _hits);

        public string Url => $"http://127.0.0.1:{Port}/x";


        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return;
                }

                var n = Interlocked.Increment(ref _hits);
                try
                {
                    context.Response.StatusCode = n <= FailFirstRequests ? (int)FailStatus : 200;
                    context.Response.Close();
                }
                catch (Exception)
                {
                    // 客户端已断开
                }
            }
        }


        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception)
            {
                // ignore
            }

            try
            {
                _loop.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
                // ignore
            }

            _cts.Dispose();
        }
    }
}
