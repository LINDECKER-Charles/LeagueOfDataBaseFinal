using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Storage;

/// <summary>
/// Reads what the page store holds from the counters of <c>prerender_unit</c> and
/// <c>prerender_build</c>, and from the pages the automatic queue wrote
/// (<c>prerender_demand</c>): listing the bucket would cost thousands of calls per epoch, and
/// the writer key cannot list.
/// </summary>
internal sealed class StorageCensus(LoDbDbContext db)
{
    /// <summary>
    /// Each (epoch, version, locale) some unit wrote pages for, with the most any of its units
    /// wrote: a pass that renders the same pages again replaces them in the store.
    /// </summary>
    public async Task<IReadOnlyList<StoredUnit>> UnitsAsync(CancellationToken cancellationToken) =>
        await db.PrerenderUnits.AsNoTracking()
            .Where(static unit => unit.PagesWritten > 0)
            .GroupBy(static unit => new { unit.Pass!.Epoch, unit.Version, unit.Locale })
            .Select(static stored => new StoredUnit(
                stored.Key.Epoch,
                stored.Key.Version,
                stored.Max(static unit => unit.PagesWritten),
                stored.Max(static unit => unit.BytesWritten)))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Each epoch the queue wrote pages into, with those no pass wrote again since: a page a
    /// pass rewrote counts under its unit, never twice.
    /// </summary>
    public async Task<IReadOnlyList<StoredQueuePages>> QueueAsync(
        CancellationToken cancellationToken) =>
        await db.PrerenderDemand.AsNoTracking()
            .Where(static page => page.State == PrerenderDemandState.Done
                && page.SupersededAt == null
                && page.Epoch != null)
            .GroupBy(static page => page.Epoch!.Value)
            .Select(static stored => new StoredQueuePages(
                stored.Key,
                stored.LongCount(),
                stored.Sum(static page => page.Bytes ?? 0)))
            .ToListAsync(cancellationToken);

    public async Task<StoredBuilds> BuildsAsync(CancellationToken cancellationToken)
    {
        var builds = db.PrerenderBuilds.AsNoTracking();
        return new StoredBuilds(
            await builds.CountAsync(cancellationToken),
            await builds.SumAsync(static build => (long)build.Files, cancellationToken),
            await builds.SumAsync(static build => build.Bytes, cancellationToken));
    }
}
