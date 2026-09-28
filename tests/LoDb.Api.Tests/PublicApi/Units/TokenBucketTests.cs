using LoDb.Api.Modules.PublicApi.Access;
using LoDb.Api.Modules.PublicApi.Limits;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.PublicApi.Units;

/// <summary>
/// go-api's token bucket, one per key: <c>rate_limit_per_min</c> tokens at most, refilled
/// continuously, and the <c>X-RateLimit-*</c> values it gives to the unit.
/// </summary>
public sealed class TokenBucketTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Noon);
    private readonly ApiRateLimiter _limiter;

    public TokenBucketTests() => _limiter = new ApiRateLimiter(_clock);

    [Fact]
    public void ABucketHoldsItsLimitThenRefuses()
    {
        var key = Key(perMinute: 10);

        var remaining = Enumerable.Range(0, 10).Select(_ => Allowed(key).Remaining).ToList();
        var refused = _limiter.Acquire(key);

        Assert.Equal([9, 8, 7, 6, 5, 4, 3, 2, 1, 0], remaining);
        Assert.Equal(new RateLimitVerdict(false, 10, 0, Unix(Noon.AddSeconds(6))), refused);
    }

    [Fact]
    public void ItRefillsContinuously()
    {
        var key = Key(perMinute: 10);
        Drain(key, 10);

        _clock.Advance(TimeSpan.FromSeconds(3));
        var halfAToken = _limiter.Acquire(key);
        _clock.Advance(TimeSpan.FromSeconds(3));
        var aToken = _limiter.Acquire(key);

        Assert.False(halfAToken.Allowed);
        Assert.Equal(Unix(Noon.AddSeconds(6)), halfAToken.Reset);
        Assert.True(aToken.Allowed);
        Assert.Equal(0, aToken.Remaining);
        Assert.False(_limiter.Acquire(key).Allowed);
    }

    [Fact]
    public void AnAllowedRequestResetsWhenTheBucketIsFullAgain()
    {
        var key = Key(perMinute: 10);

        var first = Allowed(key);
        var second = Allowed(key);

        Assert.Equal(Unix(Noon.AddSeconds(6)), first.Reset);
        Assert.Equal(Unix(Noon.AddSeconds(12)), second.Reset);
    }

    [Fact]
    public void RefillStopsAtTheLimit()
    {
        var key = Key(perMinute: 10);
        Drain(key, 10);

        _clock.Advance(TimeSpan.FromMinutes(10));
        var verdict = _limiter.Acquire(key);

        var fullAgain = Unix(_clock.GetUtcNow().AddSeconds(6));
        Assert.Equal(new RateLimitVerdict(true, 10, 9, fullAgain), verdict);
    }

    [Fact]
    public void AKeyAllowedNoRequestIsRefusedWithoutABucket()
    {
        Assert.Equal(new RateLimitVerdict(false, 0, 0, Unix(Noon)), _limiter.Acquire(Key(0)));
    }

    [Fact]
    public void EachKeyHasABucketOfItsOwn()
    {
        Drain(Key(perMinute: 10), 10);

        Assert.Equal(9, Allowed(Key(perMinute: 10, id: 2)).Remaining);
    }

    [Fact]
    public void ANewLimitStartsAFullBucket()
    {
        Drain(Key(perMinute: 10), 10);

        var upgraded = Allowed(Key(perMinute: 120));

        Assert.Equal((120, 119), (upgraded.Limit, upgraded.Remaining));
    }

    [Fact]
    public void ConcurrentRequestsNeverShareAToken()
    {
        var key = Key(perMinute: 100);
        var allowed = 0;

        Parallel.For(0, 1000, _ =>
        {
            if (_limiter.Acquire(key).Allowed)
            {
                Interlocked.Increment(ref allowed);
            }
        });

        Assert.Equal(100, allowed);
    }

    [Fact]
    public void OnlyAFullBucketIsIdle()
    {
        using var bucket = new ContinuousTokenBucket(60, _clock);
        Assert.Equal(TimeSpan.Zero, bucket.IdleDuration);

        using (bucket.AttemptAcquire())
        {
            Assert.Null(bucket.IdleDuration);
        }

        // Full again after a second: idle from then on.
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.Zero, bucket.IdleDuration);
        _clock.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal(TimeSpan.FromSeconds(4), bucket.IdleDuration);
    }

    [Fact]
    public void AskingForNoTokenTakesNone()
    {
        using var bucket = new ContinuousTokenBucket(10, _clock);

        using var probe = bucket.AttemptAcquire(0);

        Assert.True(probe.IsAcquired);
        Assert.Equal(10, bucket.GetStatistics()!.CurrentAvailablePermits);
    }

    public void Dispose() => _limiter.Dispose();

    private static ApiKeySnapshot Key(int perMinute, int id = 1) =>
        new(id, true, 500, 0, perMinute, 0, new DateOnly(2026, 9, 1), 1);

    private static long Unix(DateTimeOffset time) => time.ToUnixTimeSeconds();

    private RateLimitVerdict Allowed(ApiKeySnapshot key)
    {
        var verdict = _limiter.Acquire(key);
        Assert.True(verdict.Allowed);
        return verdict;
    }

    private void Drain(ApiKeySnapshot key, int tokens)
    {
        for (var index = 0; index < tokens; index++)
        {
            Allowed(key);
        }
    }
}
