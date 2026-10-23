using System.Diagnostics;

namespace LunarWard.Installer;

/// <summary>
/// 令牌桶限速器。
///
/// <para>上游用 <c>System.Threading.RateLimiting.TokenBucketRateLimiter</c>。
/// 该类型只在 <c>Microsoft.AspNetCore.App</c> 共享框架或独立 NuGet 包
/// <c>System.Threading.RateLimiting</c> 里，本项目的 <c>LunarWard.Installer</c>
/// 是纯 <c>Microsoft.NET.Sdk</c> 库，两者都拿不到，因此这里按同样的配额语义实现一份。</para>
///
/// <para>行为对齐上游：</para>
/// <list type="bullet">
/// <item><description>每 <see cref="ReplenishmentPeriod"/> 补充 <see cref="TokensPerPeriod"/> 个令牌；</description></item>
/// <item><description><see cref="AcquireAsync"/> 永远获取不到时返回一个"未获取"的租约，调用方按上游的
/// <c>while (!lease.IsAcquired) { await Task.Delay(1); }</c> 重试；</description></item>
/// <item><description>限速为 0 时视为不限速（令牌容量 <see cref="int.MaxValue"/>）。</description></item>
/// </list>
/// </summary>
public sealed class TokenBucketRateLimiter
{
    private readonly object _sync = new();
    private readonly double _tokensPerPeriod;
    private readonly double _tokenLimit;
    private readonly TimeSpan _replenishmentPeriod;

    private double _tokens;
    private long _lastTick;


    public TokenBucketRateLimiter(double tokenLimit, double tokensPerPeriod, TimeSpan replenishmentPeriod)
    {
        _tokenLimit = tokenLimit;
        _tokensPerPeriod = tokensPerPeriod;
        _replenishmentPeriod = replenishmentPeriod;
        _tokens = tokenLimit;
        _lastTick = Stopwatch.GetTimestamp();
    }


    /// <summary>
    /// 按"每秒字节数"构造，内部换算成 100ms 一个周期，
    /// 与上游 <c>SetRateLimiter</c> 的单位换算一致。
    /// </summary>
    public static TokenBucketRateLimiter FromBytesPerSecond(int bytesPerSecond, int bufferSize)
    {
        if (bytesPerSecond <= 0)
        {
            return new TokenBucketRateLimiter(int.MaxValue, int.MaxValue, TimeSpan.FromMilliseconds(100));
        }

        var limit = Math.Clamp(bytesPerSecond / 10, bufferSize, int.MaxValue);
        return new TokenBucketRateLimiter(limit, limit, TimeSpan.FromMilliseconds(100));
    }


    /// <summary>
    /// 尝试获取 <paramref name="count"/> 个字节的令牌。
    /// 返回 <c>true</c> 表示已扣减；<c>false</c> 表示配额不足，调用方应稍后重试。
    /// </summary>
    public bool TryAcquire(int count)
    {
        if (count <= 0)
        {
            return true;
        }

        lock (_sync)
        {
            Refill();

            if (_tokens >= count)
            {
                _tokens -= count;
                return true;
            }

            // 请求量大于桶容量时（例如单个 chunk 比 limit 还大），
            // 允许透支到 0 并返回成功，否则会永久等待。
            if (count > _tokenLimit)
            {
                _tokens = 0;
                return true;
            }

            return false;
        }
    }


    private void Refill()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = now - _lastTick;
        if (elapsed <= 0)
        {
            return;
        }

        var periods = (double)elapsed / Stopwatch.Frequency / _replenishmentPeriod.TotalSeconds;
        if (periods <= 0)
        {
            return;
        }

        _tokens = Math.Min(_tokenLimit, _tokens + periods * _tokensPerPeriod);
        _lastTick = now;
    }
}
