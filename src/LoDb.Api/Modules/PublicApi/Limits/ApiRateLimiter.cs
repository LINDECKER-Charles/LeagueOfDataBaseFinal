using System.Threading.RateLimiting;
using LoDb.Api.Modules.PublicApi.Access;

namespace LoDb.Api.Modules.PublicApi.Limits;

/// <summary>
/// One token bucket per key of <c>/v1</c>, in this instance's memory: with several
/// instances, a key gets the limit of each.
/// </summary>
/// <remarks>
/// A bucket full for a while is dropped, and the next request of its key starts a new one,
/// full as well: memory follows the keys in use, never the keys ever seen.
/// </remarks>
internal sealed class ApiRateLimiter : IDisposable
{
    private readonly TimeProvider _timeProvider;
    private readonly PartitionedRateLimiter<BucketKey> _buckets;

    public ApiRateLimiter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _buckets = PartitionedRateLimiter.Create<BucketKey, BucketKey>(key =>
            RateLimitPartition.Get(
                key,
                partition => new ContinuousTokenBucket(partition.PerMinute, timeProvider)));
    }

    /// <summary>Takes a token from the key's bucket, if it holds one.</summary>
    public RateLimitVerdict Acquire(ApiKeySnapshot key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.RateLimitPerMin <= 0)
        {
            return RateLimitVerdict.Closed(_timeProvider.GetUtcNow());
        }

        using var lease = _buckets.AttemptAcquire(new BucketKey(key.Id, key.RateLimitPerMin));
        return lease.TryGetMetadata(RateLimitVerdict.MetadataName, out var verdict)
            && verdict is not null
                ? verdict
                : throw new InvalidOperationException("A bucket answered without a verdict.");
    }

    public void Dispose() => _buckets.Dispose();
}
