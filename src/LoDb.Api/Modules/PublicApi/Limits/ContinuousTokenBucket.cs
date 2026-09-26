using System.Threading.RateLimiting;

namespace LoDb.Api.Modules.PublicApi.Limits;

/// <summary>
/// go-api's token bucket: <c>rate_limit_per_min</c> tokens at most, refilled continuously at
/// that many per minute, a request taking one.
/// </summary>
/// <remarks>
/// The built-in token bucket only adds whole tokens per period, and ten a minute is not a
/// whole number per second: the fractional one keeps the <c>X-RateLimit-*</c> values of
/// go-api to the unit. A refusal never queues.
/// </remarks>
internal sealed class ContinuousTokenBucket : RateLimiter
{
    private const double SecondsPerMinute = 60;

    private readonly Lock _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly int _capacity;
    private readonly double _perSecond;
    private double _tokens;
    private long _lastFill;
    private long? _fullSince;

    public ContinuousTokenBucket(int perMinute, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(perMinute, 1);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        _capacity = perMinute;
        _perSecond = perMinute / SecondsPerMinute;
        _tokens = perMinute;
        _lastFill = timeProvider.GetTimestamp();
        _fullSince = _lastFill;
    }

    /// <summary>
    /// How long the bucket has been full: only then may it be dropped, a new one being the
    /// same.
    /// </summary>
    public override TimeSpan? IdleDuration
    {
        get
        {
            lock (_gate)
            {
                var now = _timeProvider.GetTimestamp();
                Refill(now);
                return _fullSince is { } since ? _timeProvider.GetElapsedTime(since, now) : null;
            }
        }
    }

    public override RateLimiterStatistics? GetStatistics()
    {
        lock (_gate)
        {
            Refill(_timeProvider.GetTimestamp());
            return new RateLimiterStatistics { CurrentAvailablePermits = (long)_tokens };
        }
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(permitCount);
        lock (_gate)
        {
            var wallClock = _timeProvider.GetUtcNow();
            Refill(_timeProvider.GetTimestamp());
            // A request of zero permits only asks whether one would be granted.
            if (_tokens < Math.Max(permitCount, 1))
            {
                return new VerdictLease(new RateLimitVerdict(
                    Allowed: false,
                    Limit: _capacity,
                    Remaining: 0,
                    Reset: ResetAt(1, wallClock)));
            }

            _tokens -= permitCount;
            if (_tokens < _capacity)
            {
                _fullSince = null;
            }

            return new VerdictLease(new RateLimitVerdict(
                Allowed: true,
                Limit: _capacity,
                Remaining: (int)_tokens,
                Reset: ResetAt(_capacity, wallClock)));
        }
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(AttemptAcquireCore(permitCount));

    private void Refill(long now)
    {
        var elapsed = _timeProvider.GetElapsedTime(_lastFill, now).TotalSeconds;
        _tokens = Math.Min(_capacity, _tokens + (elapsed * _perSecond));
        _lastFill = now;
        if (_tokens >= _capacity)
        {
            _fullSince ??= now;
        }
    }

    // When the bucket holds target tokens, in whole seconds from now, as a Unix time.
    private long ResetAt(double target, DateTimeOffset now)
    {
        var missing = target - _tokens;
        if (missing <= 0)
        {
            return now.ToUnixTimeSeconds();
        }

        return now.AddSeconds(Math.Ceiling(missing / _perSecond)).ToUnixTimeSeconds();
    }

    private sealed class VerdictLease(RateLimitVerdict verdict) : RateLimitLease
    {
        public override bool IsAcquired => verdict.Allowed;

        public override IEnumerable<string> MetadataNames => [RateLimitVerdict.MetadataName.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (string.Equals(
                metadataName,
                RateLimitVerdict.MetadataName.Name,
                StringComparison.Ordinal))
            {
                metadata = verdict;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
