using LoDb.Api.Modules.Admin.Prerender.Demand.Validation;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Queue;

/// <summary>
/// The queue's worker's reads and writes of <c>prerender_demand</c>: the most requested
/// pending pages, a page taken, a page ended, the rows an epoch change or a restart left behind.
/// </summary>
/// <remarks>
/// A page that waits (a failed try, a catalogue not stored, a version still held for the latest
/// one) keeps its reason and is taken again <see cref="RetryAfter"/> later: never in a loop.
/// </remarks>
internal sealed class DemandQueue(IDbContextFactory<LoDbDbContext> contexts, TimeProvider clock)
{
    /// <summary>How long a page that waits stays out of the worker's batches.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Up to <paramref name="count"/> pending pages, the most requested first, the oldest first
    /// among equals; none that waits.
    /// </summary>
    public async Task<IReadOnlyList<DemandRow>> NextAsync(int count, CancellationToken token)
    {
        var waited = clock.GetUtcNow() - RetryAfter;
        await using var db = await contexts.CreateDbContextAsync(token);
        return await db.PrerenderDemand.AsNoTracking()
            .Where(page => page.State == PrerenderDemandState.Pending
                && (page.Reason == null || page.UpdatedAt < waited))
            .OrderByDescending(static page => page.Demand)
            .ThenBy(static page => page.FirstSeenAt)
            .Take(count)
            .Select(static page => new DemandRow(page.Path, page.Demand, page.Attempts))
            .ToListAsync(token);
    }

    /// <summary>Takes a pending page; false when another worker took it.</summary>
    public async Task<bool> ClaimAsync(string path, CancellationToken token)
    {
        await using var db = await contexts.CreateDbContextAsync(token);
        var taken = await db.PrerenderDemand
            .Where(page => page.Path == path && page.State == PrerenderDemandState.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(page => page.State, PrerenderDemandState.Rendering)
                    .SetProperty(page => page.UpdatedAt, clock.GetUtcNow()),
                token);
        return taken == 1;
    }

    /// <summary>
    /// Ends a page taken by <see cref="ClaimAsync"/>, for <paramref name="epoch"/>: a written
    /// page keeps its bytes and class, the queue's own again until a pass writes it anew.
    /// </summary>
    public async Task EndAsync(string path, DemandEnd end, int epoch, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(end);
        var now = clock.GetUtcNow();
        var isWritten = end.State == PrerenderDemandState.Done;
        int? endedIn = end.State == PrerenderDemandState.Pending ? null : epoch;
        DateTimeOffset? writtenAt = isWritten ? now : null;
        long? bytes = isWritten ? end.Bytes : null;
        var storedClass = isWritten ? end.StoredClass : null;
        var tries = end.IsFailedTry ? 1 : 0;
        var reason = DiagnosticText.Clip(end.Reason, PrerenderDemandPage.ReasonMaxLength);
        await using var db = await contexts.CreateDbContextAsync(token);
        await db.PrerenderDemand.Where(page => page.Path == path).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(page => page.State, end.State)
                .SetProperty(page => page.Attempts, page => page.Attempts + tries)
                .SetProperty(page => page.Epoch, endedIn)
                .SetProperty(page => page.RenderedAt, page => writtenAt ?? page.RenderedAt)
                .SetProperty(page => page.Reason, reason)
                .SetProperty(page => page.Bytes, bytes)
                .SetProperty(page => page.StoredClass, storedClass)
                .SetProperty(page => page.SupersededAt, (DateTimeOffset?)null)
                .SetProperty(page => page.UpdatedAt, now),
            token);
    }

    /// <summary>
    /// Puts back to pending the pages a stopped worker left rendering, and those ended in
    /// another epoch than <paramref name="epoch"/>, the served one: the store serves another,
    /// and what the queue wrote in theirs no longer counts as the queue's.
    /// </summary>
    public async Task<int> ReviveAsync(int epoch, CancellationToken token)
    {
        await using var db = await contexts.CreateDbContextAsync(token);
        return await db.PrerenderDemand
            .Where(page => page.State == PrerenderDemandState.Rendering
                || (page.State != PrerenderDemandState.Pending && page.Epoch != epoch))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(page => page.State, PrerenderDemandState.Pending)
                    .SetProperty(page => page.Attempts, 0)
                    .SetProperty(page => page.Epoch, (int?)null)
                    .SetProperty(page => page.Reason, (string?)null)
                    .SetProperty(page => page.Bytes, (long?)null)
                    .SetProperty(page => page.StoredClass, (string?)null)
                    .SetProperty(page => page.SupersededAt, (DateTimeOffset?)null)
                    .SetProperty(page => page.UpdatedAt, clock.GetUtcNow()),
                token);
    }

    /// <summary>
    /// Whether a pass stored <paramref name="page"/> in <paramref name="epoch"/>.
    /// </summary>
    public async Task<bool> IsStoredAsync(
        DemandPath page,
        VersionPageIndex index,
        int epoch,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(page);
        await using var db = await contexts.CreateDbContextAsync(token);
        var units = await ServedUnits.ReadAsync(db, epoch, [page.Version.Value], token);
        return units.Holds(page, index);
    }

    /// <summary>The pages pending now, for the metrics.</summary>
    public async Task<long> PendingAsync(CancellationToken token)
    {
        await using var db = await contexts.CreateDbContextAsync(token);
        return await db.PrerenderDemand.LongCountAsync(
            static page => page.State == PrerenderDemandState.Pending,
            token);
    }
}
