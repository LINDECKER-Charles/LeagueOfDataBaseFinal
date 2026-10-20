using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Http.History;

/// <summary>
/// Reads the passes the admin shows, newest first, with their units by state and the names of
/// their authors, and the newer epoch a complete pass made ready to serve.
/// </summary>
internal sealed class PassHistory(LoDbDbContext db)
{
    /// <summary>The passes a report shows: years of them at a pass a week or so.</summary>
    public const int MaxPasses = 50;

    private static readonly IReadOnlyDictionary<PrerenderUnitState, int> NoUnits =
        new Dictionary<PrerenderUnitState, int>();

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
    /// The newest epoch above <paramref name="servedEpoch"/> that a pass over every older patch
    /// completed and none writes now: the one to serve next. Null when none.
    /// </summary>
    public async Task<int?> ReadyEpochAsync(
        int servedEpoch,
        int? writingEpoch,
        CancellationToken cancellationToken)
    {
        var completed = await db.PrerenderPasses.AsNoTracking()
            .Where(pass => pass.Epoch > servedEpoch
                && pass.State == PrerenderPassState.Completed)
            .Select(static pass => new { pass.Epoch, pass.Scope })
            .ToListAsync(cancellationToken);
        return completed
            .Where(pass => pass.Epoch != writingEpoch && PassScopeJson.Read(pass.Scope).IsWhole)
            .Select(static pass => (int?)pass.Epoch)
            .Max();
    }

    private async Task<IReadOnlyList<PassFacts>> FactsOfAsync(
        List<PrerenderPass> passes,
        CancellationToken cancellationToken)
    {
        var units = await UnitsAsync([.. passes.Select(static pass => pass.Id)], cancellationToken);
        var authors = await AuthorsAsync(passes, cancellationToken);
        return
        [
            .. passes.Select(pass => new PassFacts
            {
                Pass = pass,
                Units = units.GetValueOrDefault(pass.Id) ?? NoUnits,
                AuthorName = pass.AuthorId is { } id ? authors.GetValueOrDefault(id) : null,
            }),
        ];
    }

    private async Task<Dictionary<long, IReadOnlyDictionary<PrerenderUnitState, int>>> UnitsAsync(
        List<long> passIds,
        CancellationToken cancellationToken)
    {
        var counts = await db.PrerenderUnits.AsNoTracking()
            .Where(unit => passIds.Contains(unit.PassId))
            .GroupBy(static unit => new { unit.PassId, unit.State })
            .Select(static group =>
                new { group.Key.PassId, group.Key.State, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return counts
            .GroupBy(static count => count.PassId)
            .ToDictionary(
                static pass => pass.Key,
                static IReadOnlyDictionary<PrerenderUnitState, int> (pass) => pass.ToDictionary(
                    static count => count.State,
                    static count => count.Count));
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
}
