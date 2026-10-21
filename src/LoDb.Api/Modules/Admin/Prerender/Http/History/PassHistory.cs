using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Http.History;

/// <summary>
/// Reads the passes the admin shows, newest first, with their units by state and the names of
/// their authors, and the epochs passes wrote above the served one.
/// </summary>
internal sealed class PassHistory(LoDbDbContext db)
{
    /// <summary>The passes a report shows: years of them at a pass a week or so.</summary>
    public const int MaxPasses = 50;

    /// <summary>
    /// The latest passes, newest first. The active one is among them: no pass opens while it
    /// is active, so it is the newest.
    /// </summary>
    public async Task<IReadOnlyList<PassFacts>> LatestAsync(CancellationToken cancellationToken)
    {
        var passes = await db.PrerenderPasses.AsNoTracking()
            .OrderByDescending(static pass => pass.CreatedAt)
            .ThenByDescending(static pass => pass.Id)
            .Take(MaxPasses)
            .ToListAsync(cancellationToken);
        return await FactsOfAsync(passes, cancellationToken);
    }

    /// <summary>The pass <paramref name="id"/>, any state; null when there is none.</summary>
    public async Task<PassFacts?> OneAsync(long id, CancellationToken cancellationToken)
    {
        var pass = await db.PrerenderPasses.AsNoTracking()
            .SingleOrDefaultAsync(found => found.Id == id, cancellationToken);
        return pass is null ? null : (await FactsOfAsync([pass], cancellationToken))[0];
    }

    /// <summary>
    /// The epochs some pass writes or wrote above <paramref name="servedEpoch"/>: the one the
    /// active pass writes, the complete ones awaiting the switch, the leftovers of cancelled
    /// passes.
    /// </summary>
    public async Task<IReadOnlyList<int>> EpochsAboveAsync(
        int servedEpoch,
        CancellationToken cancellationToken) =>
        await db.PrerenderPasses.AsNoTracking()
            .Where(pass => pass.Epoch > servedEpoch)
            .Select(static pass => pass.Epoch)
            .Distinct()
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<PassFacts>> FactsOfAsync(
        List<PrerenderPass> passes,
        CancellationToken cancellationToken)
    {
        var units = await UnitsAsync([.. passes.Select(static pass => pass.Id)], cancellationToken);
        var authors = await AuthorsAsync(passes, cancellationToken);
        return
        [
            .. passes.Select(pass => FactsOf(
                pass,
                units.GetValueOrDefault(pass.Id) ?? [],
                pass.AuthorId is { } id ? authors.GetValueOrDefault(id) : null)),
        ];
    }

    private static PassFacts FactsOf(PrerenderPass pass, List<StateCount> counts, string? author)
    {
        var pending = counts.Find(static count => count.State == PrerenderUnitState.Pending);
        return new PassFacts
        {
            Pass = pass,
            Units = counts.ToDictionary(static count => count.State, static count => count.Count),
            AuthorName = author,
            ExpectedPages = pending?.Expected ?? 0,
            UnexpectedUnits = pending?.Unexpected ?? 0,
        };
    }

    // One group per pass and state, with the expected pages of its units: the pending units'
    // are what is left of the pass beyond the listed ones.
    private async Task<Dictionary<long, List<StateCount>>> UnitsAsync(
        List<long> passIds,
        CancellationToken cancellationToken)
    {
        var counts = await db.PrerenderUnits.AsNoTracking()
            .Where(unit => passIds.Contains(unit.PassId))
            .GroupBy(static unit => new { unit.PassId, unit.State })
            .Select(static group => new StateCount
            {
                PassId = group.Key.PassId,
                State = group.Key.State,
                Count = group.Count(),
                Expected = group.Sum(static unit => (long?)unit.PagesExpected) ?? 0,
                Unexpected = group.Count(static unit => unit.PagesExpected == null),
            })
            .ToListAsync(cancellationToken);
        return counts.GroupBy(static count => count.PassId)
            .ToDictionary(static pass => pass.Key, static pass => pass.ToList());
    }

    private async Task<Dictionary<int, string?>> AuthorsAsync(
        List<PrerenderPass> passes,
        CancellationToken cancellationToken)
    {
        var ids = passes
            .Where(static pass => pass.AuthorId is not null)
            .Select(static pass => pass.AuthorId!.Value)
            .Distinct()
            .ToList();
        return await db.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(
                static user => user.Id,
                static user => user.UserName,
                cancellationToken);
    }

    // The units of a pass in one state.
    private sealed record StateCount
    {
        public long PassId { get; init; }

        public PrerenderUnitState State { get; init; }

        public int Count { get; init; }

        public long Expected { get; init; }

        public int Unexpected { get; init; }
    }
}
