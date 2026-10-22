using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Revision.Queue;

/// <summary>
/// The drain's reads and writes of <c>prerender_revision</c>: the pending pages that are due, a
/// page taken, a page ended, the pages a stopped worker left rendering.
/// </summary>
/// <remarks>
/// The states and their meaning are the automatic queue's (<see cref="DemandEnd"/>). A page
/// that waits (a failed try, a version still held for the latest one) is due again
/// <see cref="RetryAfter"/> later: never in a loop.
/// </remarks>
internal sealed class RevisionQueue(IDbContextFactory<LoDbDbContext> contexts, TimeProvider clock)
{
    /// <summary>How long a page that waits stays out of the drain's batches.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Up to <paramref name="count"/> pending pages that are due, the first due first, then by
    /// version and locale: the pages of a (version, locale) one after the other, the catalog of
    /// each loaded once by the API.
    /// </summary>
    public async Task<IReadOnlyList<RevisionRow>> NextAsync(int count, CancellationToken token)
    {
        var now = clock.GetUtcNow();
        await using var db = await contexts.CreateDbContextAsync(token);
        return await db.PrerenderRevisions.AsNoTracking()
            .Where(page => page.State == PrerenderDemandState.Pending && page.DueAt <= now)
            .OrderBy(static page => page.DueAt)
            .ThenBy(static page => page.Version)
            .ThenBy(static page => page.Locale)
            .ThenBy(static page => page.Path)
            .Take(count)
            .Select(static page => new RevisionRow(page.Path, page.Epoch, page.Attempts))
            .ToListAsync(token);
    }

    /// <summary>Takes a pending page; false when another worker took it.</summary>
    public async Task<bool> ClaimAsync(RevisionRow row, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(row);
        await using var db = await contexts.CreateDbContextAsync(token);
        var taken = await db.PrerenderRevisions
            .Where(page => page.Path == row.Path
                && page.Epoch == row.Epoch
                && page.State == PrerenderDemandState.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(page => page.State, PrerenderDemandState.Rendering)
                    .SetProperty(page => page.UpdatedAt, clock.GetUtcNow()),
                token);
        return taken == 1;
    }

    /// <summary>Ends a page taken by <see cref="ClaimAsync"/>, or one judged before.</summary>
    public async Task EndAsync(RevisionRow row, DemandEnd end, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(end);
        var now = clock.GetUtcNow();
        DateTimeOffset? writtenAt = end.State == PrerenderDemandState.Done ? now : null;
        DateTimeOffset? dueAgain = end.State == PrerenderDemandState.Pending
            ? now + RetryAfter
            : null;
        var tries = end.IsFailedTry ? 1 : 0;
        var reason = DiagnosticText.Clip(end.Reason, PrerenderDemandPage.ReasonMaxLength);
        await using var db = await contexts.CreateDbContextAsync(token);
        await db.PrerenderRevisions
            .Where(page => page.Path == row.Path && page.Epoch == row.Epoch)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(page => page.State, end.State)
                    .SetProperty(page => page.Attempts, page => page.Attempts + tries)
                    .SetProperty(page => page.DueAt, page => dueAgain ?? page.DueAt)
                    .SetProperty(page => page.WrittenAt, page => writtenAt ?? page.WrittenAt)
                    .SetProperty(page => page.Reason, reason)
                    .SetProperty(page => page.UpdatedAt, now),
                token);
    }

    /// <summary>Puts back to pending the pages a stopped worker left rendering.</summary>
    public async Task<int> ReviveAsync(CancellationToken token)
    {
        await using var db = await contexts.CreateDbContextAsync(token);
        return await db.PrerenderRevisions
            .Where(static page => page.State == PrerenderDemandState.Rendering)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(page => page.State, PrerenderDemandState.Pending)
                    .SetProperty(page => page.UpdatedAt, clock.GetUtcNow()),
                token);
    }

    /// <summary>The pages pending now, due or not, for the metrics.</summary>
    public async Task<long> PendingAsync(CancellationToken token)
    {
        await using var db = await contexts.CreateDbContextAsync(token);
        return await db.PrerenderRevisions.LongCountAsync(
            static page => page.State == PrerenderDemandState.Pending,
            token);
    }
}
