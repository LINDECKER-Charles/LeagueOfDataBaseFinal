using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Security.Admission;

/// <summary>
/// The asynchronous admission to password hashing: a request that is about to hash first
/// takes a permit here, before any call to Identity.
/// </summary>
/// <remarks>
/// <para>
/// <c>IPasswordHasher</c> is synchronous: waiting there for a limiter would block a thread of
/// the pool per request, and a flood of sign-ins would starve the whole API. Here the wait
/// is a queued task, bounded in length and in time, and the surplus is refused at once.
/// </para>
/// <para>
/// The permit covers every hash of the request: a check and the rewrite of a legacy hash
/// that follows it. Its lease is disposed once the request no longer hashes.
/// </para>
/// </remarks>
internal sealed class HashingAdmission : IDisposable
{
    private const int OnePermit = 1;

    private readonly ConcurrencyLimiter _limiter;
    private readonly HashingAdmissionOptions _options;
    private readonly SecurityMetrics _metrics;
    private readonly TimeProvider _clock;

    public HashingAdmission(
        IOptions<HashingAdmissionOptions> options,
        SecurityMetrics metrics,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _metrics = metrics;
        _clock = clock;
        _limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = _options.Permits,
            QueueLimit = _options.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    /// <summary>Waits for a permit, which the caller disposes once done hashing.</summary>
    /// <exception cref="HashingBusyException">
    /// The queue is full, or no permit came within the wait.
    /// </exception>
    public async Task<RateLimitLease> EnterAsync(CancellationToken cancellationToken)
    {
        var started = _clock.GetTimestamp();
        var lease = await AcquireAsync(started, cancellationToken);
        _metrics.RecordWait(_clock.GetElapsedTime(started));
        if (lease.IsAcquired)
        {
            return lease;
        }

        lease.Dispose();
        throw Refused(SecurityMetrics.QueueFull);
    }

    public void Dispose() => _limiter.Dispose();

    private async Task<RateLimitLease> AcquireAsync(
        long started,
        CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(_options.MaxWait, _clock);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadline.Token);
        try
        {
            return await _limiter.AcquireAsync(OnePermit, wait.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _metrics.RecordWait(_clock.GetElapsedTime(started));
            throw Refused(SecurityMetrics.WaitTimeout);
        }
    }

    private HashingBusyException Refused(string reason)
    {
        _metrics.RecordHashingRefused(reason);
        return new HashingBusyException(_options.RetryAfter);
    }
}
