using LoDb.Infrastructure.Locks;

namespace LoDb.Ingestion.Catalog.States.Updates;

/// <summary>
/// The advisory lock every write of the index takes, <c>catalog:states</c>: one writer at a
/// time across the API's instances and the build command.
/// </summary>
/// <remarks>
/// The lock is tried again every half second within the wait the caller gives: a promotion
/// waits a few seconds for a reconciliation that is finishing, then leaves the work to it.
/// </remarks>
internal sealed class StateIndexLock(IDistributedLock locks, TimeProvider timeProvider)
{
    public const string Name = "catalog:states";

    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(500);

    /// <returns>The held lock, released when disposed; null once the wait is spent.</returns>
    public async Task<IAsyncDisposable?> AcquireAsync(
        TimeSpan wait,
        CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        while (true)
        {
            var held = await locks.TryAcquireAsync(Name, cancellationToken).ConfigureAwait(false);
            if (held is not null || timeProvider.GetElapsedTime(started) + RetryInterval > wait)
            {
                return held;
            }

            await Task.Delay(RetryInterval, timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}
