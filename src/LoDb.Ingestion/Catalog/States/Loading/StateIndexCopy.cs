using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Ingestion.Catalog.States.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Catalog.States.Loading;

/// <summary>
/// The copy of the index of states a process holds, which reloads itself: what every reader of
/// <see cref="IEntityStateIndex"/> gets.
/// </summary>
/// <remarks>
/// <para>
/// The first read loads the current generation; concurrent reads share that load. Then, at
/// most every <c>LoDb:Catalog:VersionsLifetime</c>, a read checks the stamp of the current
/// generation (its id and coverage end, one row) and reloads when it moved: a rebuild or an
/// extension, by this process or another one (the <c>ingest</c> command can promote). Reads
/// meanwhile get the copy held, never wait for the check. A write of this process invalidates
/// the copy, so that its next read checks at once.
/// </para>
/// <para>
/// A failed check or load, whatever failed it (a database out of reach, a row it cannot
/// read), keeps the copy held, checked again a lifetime later. Without a copy, the read throws
/// <see cref="StateIndexUnavailableException"/> (the API's 503), and the next one tries again;
/// each failure is one line and one count.
/// </para>
/// </remarks>
internal sealed class StateIndexCopy(
    StoredEntityStateIndex stored,
    StateIndexStamps stamps,
    IOptions<CatalogOptions> options,
    StateIndexMetrics metrics,
    TimeProvider timeProvider,
    ILogger<StateIndexCopy> logger) : IEntityStateIndex
{
    private const long NothingSettled = -1;

    private readonly Lock gate = new();
    private EntityStateSnapshot? held;
    private long checkedAt;

    // The writes of this process, and those the copy held has seen: a copy older than a write
    // is never served while a refresh that follows the write can give a newer one.
    private long writes;
    private long settledWrites = NothingSettled;
    private Refresh? refreshing;

    public async ValueTask<EntityStateSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        Task<EntityStateSnapshot?> pending;
        lock (gate)
        {
            if (held is { } copy && settledWrites == writes && (refreshing is not null || !IsDue()))
            {
                return copy;
            }

            pending = Join().Pending;
        }

        var snapshot = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            metrics.RecordUnavailable();
            throw new StateIndexUnavailableException(
                "The index of states could not be read, and no copy of it is held.",
                new TimeoutException("The last load of the index failed."));
        }

        return snapshot;
    }

    /// <summary>Has the next read check the stamp at once: this process wrote the index.</summary>
    public void Invalidate()
    {
        lock (gate)
        {
            writes++;
        }
    }

    // Under the gate.
    private bool IsDue() =>
        timeProvider.GetElapsedTime(checkedAt) >= options.Value.VersionsLifetime;

    // Under the gate: the refresh that follows the last write, started when none does. It runs
    // off the gate, so that one ending at once settles under it.
    private Refresh Join()
    {
        if (refreshing is { } current && current.Writes == writes)
        {
            return current;
        }

        var known = held;
        var seen = writes;
        refreshing = new Refresh(Task.Run(() => RefreshAsync(known, seen)), seen);
        return refreshing;
    }

    // Never cancelled by a reader: the readers that join it would all fail. The database's
    // timeouts bound it. Every failure settles it: a refresh left faulted would stay the one
    // reads join, and the copy would never be checked again.
    private async Task<EntityStateSnapshot?> RefreshAsync(EntityStateSnapshot? copy, long seen)
    {
        try
        {
            var isCurrent = await IsCurrentAsync(copy).ConfigureAwait(false);
            var fresh = isCurrent ? copy : await LoadAsync().ConfigureAwait(false);
            return Settle(fresh, seen);
        }
        catch (Exception exception)
        {
            metrics.RecordCopyFailed();
            StateIndexLog.CopyFailed(logger, copy is not null, exception);
            return Settle(copy, seen);
        }
    }

    // The stamp of the current generation is the copy's: nothing to load.
    private async Task<bool> IsCurrentAsync(EntityStateSnapshot? copy) =>
        copy is not null
        && Equals(await stamps.ReadAsync(CancellationToken.None).ConfigureAwait(false), copy.Stamp);

    private async Task<EntityStateSnapshot> LoadAsync()
    {
        var started = timeProvider.GetTimestamp();
        var snapshot = await stored.GetAsync(CancellationToken.None).ConfigureAwait(false);
        var elapsed = timeProvider.GetElapsedTime(started);
        metrics.RecordCopyLoaded();
        StateIndexLog.CopyLoaded(
            logger,
            snapshot.Generation?.Id,
            snapshot.Coverage.End?.Value,
            snapshot.States.Count,
            (long)elapsed.TotalMilliseconds);
        return snapshot;
    }

    // Keeps the snapshot of the newest refresh (the held one after a failure, none after a
    // first failed load), and dates the check.
    private EntityStateSnapshot? Settle(EntityStateSnapshot? snapshot, long seen)
    {
        lock (gate)
        {
            if (snapshot is not null && seen >= settledWrites)
            {
                held = snapshot;
                settledWrites = seen;
                checkedAt = timeProvider.GetTimestamp();
            }

            if (refreshing?.Writes == seen)
            {
                refreshing = null;
            }

            return held;
        }
    }

    private sealed record Refresh(Task<EntityStateSnapshot?> Pending, long Writes);
}
