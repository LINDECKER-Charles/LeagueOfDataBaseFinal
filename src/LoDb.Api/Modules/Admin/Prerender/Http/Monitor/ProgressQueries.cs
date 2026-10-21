using LoDb.Api.Modules.Admin.Prerender.Http.Views.Progress;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Monitor;

/// <summary>
/// The reads of the live monitor, each served by an index of <c>prerender_unit</c>: the units
/// running and next by state (<c>ix_prerender_unit_next</c>), the last ended and the failed
/// ones by their end (<c>ix_prerender_unit_ended</c>), the versions by the key's prefix.
/// </summary>
/// <remarks>
/// Nothing reads by <c>updated_at</c> through an index: rewritten at each page, it has none.
/// The versions changed since a time are found among the pass's units by the key's prefix,
/// some 8,400 rows for a whole pass, a few milliseconds.
/// </remarks>
internal sealed class ProgressQueries(LoDbDbContext db)
{
    /// <summary>The next units shown.</summary>
    public const int MaxNext = 5;

    /// <summary>The units that ended last shown.</summary>
    public const int MaxRecent = 12;

    /// <summary>The units with failures shown.</summary>
    public const int MaxFailures = 20;

    /// <summary>The locales the pass has units in, in the site's order.</summary>
    public async Task<IReadOnlyList<string>> LocalesAsync(long passId, CancellationToken token)
    {
        var codes = await Units(passId).Select(static unit => unit.Locale)
            .Distinct()
            .ToListAsync(token);
        return [.. UiLocales.All.Select(UiLocales.Code).Where(codes.Contains)];
    }

    /// <summary>
    /// The units of every version of the pass; with <paramref name="since"/>, of the versions
    /// one unit of which changed after it, each read whole.
    /// </summary>
    public Task<List<PrerenderUnit>> VersionUnitsAsync(
        long passId,
        DateTimeOffset? since,
        CancellationToken token)
    {
        var units = Units(passId);
        if (since is { } from)
        {
            var changed = Units(passId).Where(unit => unit.UpdatedAt > from)
                .Select(static unit => unit.Version)
                .Distinct();
            units = units.Where(unit => changed.Contains(unit.Version));
        }

        return units.ToListAsync(token);
    }

    public async Task<IReadOnlyList<PrerenderUnitView>> RunningAsync(
        long passId,
        CancellationToken token) =>
        Views(await Units(passId)
            .Where(static unit => unit.State == PrerenderUnitState.Running)
            .ToListAsync(token));

    /// <summary>
    /// The next pending units, in the order of the worker (<c>UnitQueue.NextAsync</c>), but
    /// those of the <paramref name="waiting"/> versions, which it takes later.
    /// </summary>
    public async Task<IReadOnlyList<PrerenderUnitKey>> NextAsync(
        long passId,
        IReadOnlyCollection<string> waiting,
        CancellationToken token)
    {
        var skipped = waiting.ToList();
        return await Units(passId)
            .Where(unit => unit.State == PrerenderUnitState.Pending
                && !skipped.Contains(unit.Version))
            .OrderBy(static unit => unit.Priority)
            .ThenBy(static unit => unit.Version)
            .ThenBy(static unit => unit.Locale)
            .Take(MaxNext)
            .Select(static unit => new PrerenderUnitKey
            {
                Version = unit.Version,
                Locale = unit.Locale,
            })
            .ToListAsync(token);
    }

    /// <summary>The units that ended last, completed or failed, newest first.</summary>
    public async Task<IReadOnlyList<PrerenderUnitView>> RecentAsync(
        long passId,
        CancellationToken token) =>
        Views(await Ended(passId)
            .OrderByDescending(static unit => unit.EndedAt)
            .Take(MaxRecent)
            .ToListAsync(token));

    /// <summary>The units that failed or ended with failed pages, newest first.</summary>
    public async Task<IReadOnlyList<PrerenderUnitView>> FailuresAsync(
        long passId,
        CancellationToken token) =>
        Views(await Failed(passId)
            .OrderByDescending(static unit => unit.EndedAt)
            .Take(MaxFailures)
            .ToListAsync(token));

    /// <summary>Every unit that failed or ended with failed pages.</summary>
    public Task<int> FailureCountAsync(long passId, CancellationToken token) =>
        Failed(passId).CountAsync(token);

    private IQueryable<PrerenderUnit> Units(long passId) =>
        db.PrerenderUnits.AsNoTracking().Where(unit => unit.PassId == passId);

    private IQueryable<PrerenderUnit> Ended(long passId) =>
        Units(passId).Where(static unit => unit.EndedAt != null);

    private IQueryable<PrerenderUnit> Failed(long passId) =>
        Ended(passId).Where(static unit =>
            unit.State == PrerenderUnitState.Failed || unit.PagesFailed > 0);

    private static List<PrerenderUnitView> Views(List<PrerenderUnit> units) =>
        [.. units.Select(UnitViews.Of)];
}
