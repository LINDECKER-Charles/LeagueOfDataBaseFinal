using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Infrastructure.Persistence.Prerender.Revision;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Revision.Http;

/// <summary>
/// Reads the queue of the closed states for the admin: its counts by epoch and state, the
/// pages written lately (on their own index), the pending pages due now and the next one (on
/// the drain's), and the producer's watermark.
/// </summary>
internal sealed class RevisionReader(
    IDbContextFactory<LoDbDbContext> contexts,
    PrerenderActivation activation,
    IOptions<PrerenderOptions> options,
    TimeProvider clock)
{
    public async Task<PrerenderRevisionReport> ReadAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var epochs = await EpochsAsync(db, cancellationToken);
        var pending = Pending(db);
        return new PrerenderRevisionReport
        {
            GeneratedAt = now,
            IsActive = activation.IsActive,
            ServedEpoch = options.Value.ServedEpoch,
            Pending = epochs.Sum(static epoch => epoch.Pending),
            Due = await pending.LongCountAsync(page => page.DueAt <= now, cancellationToken),
            Done = epochs.Sum(static epoch => epoch.Done),
            Failed = epochs.Sum(static epoch => epoch.Failed),
            Skipped = epochs.Sum(static epoch => epoch.Skipped),
            WrittenLastHour = await WrittenSinceAsync(db, now.AddHours(-1), cancellationToken),
            WrittenLastDay = await WrittenSinceAsync(db, now.AddDays(-1), cancellationToken),
            NextDueAt = await pending.MinAsync(
                static page => (DateTimeOffset?)page.DueAt,
                cancellationToken),
            ClosedThrough = await db.PrerenderRevisionWatermarks.AsNoTracking()
                .Where(static mark => mark.GenerationId > 0)
                .Select(static mark => (DateTimeOffset?)mark.ClosedThrough)
                .SingleOrDefaultAsync(cancellationToken),
            Epochs = epochs,
        };
    }

    private static async Task<IReadOnlyList<PrerenderRevisionEpoch>> EpochsAsync(
        LoDbDbContext db,
        CancellationToken cancellationToken)
    {
        var counts = await db.PrerenderRevisions.AsNoTracking()
            .GroupBy(static page => new { page.Epoch, page.State })
            .Select(static group => new StateCount(
                group.Key.Epoch,
                group.Key.State,
                group.LongCount()))
            .ToListAsync(cancellationToken);
        return
        [
            .. counts.GroupBy(static count => count.Epoch)
                .OrderByDescending(static epoch => epoch.Key)
                .Select(static epoch => new PrerenderRevisionEpoch
                {
                    Epoch = epoch.Key,
                    Pending = PagesIn(epoch, PrerenderDemandState.Pending),
                    Done = PagesIn(epoch, PrerenderDemandState.Done),
                    Failed = PagesIn(epoch, PrerenderDemandState.Failed),
                    Skipped = PagesIn(epoch, PrerenderDemandState.Skipped),
                }),
        ];
    }

    // A page rendering counts as pending: it is not written yet.
    private static long PagesIn(IEnumerable<StateCount> counts, PrerenderDemandState state) =>
        counts.Where(count => count.State == state
                || (state == PrerenderDemandState.Pending
                    && count.State == PrerenderDemandState.Rendering))
            .Sum(static count => count.Pages);

    private static IQueryable<PrerenderRevisionPage> Pending(LoDbDbContext db) =>
        db.PrerenderRevisions.AsNoTracking()
            .Where(static page => page.State == PrerenderDemandState.Pending);

    private static Task<long> WrittenSinceAsync(
        LoDbDbContext db,
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        db.PrerenderRevisions.LongCountAsync(page => page.WrittenAt >= since, cancellationToken);

    private sealed record StateCount(int Epoch, PrerenderDemandState State, long Pages);
}
