using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Passes.Planning;

/// <summary>
/// The pages a unit of each version lists over a set of types of page, as a unit of a former
/// pass over the same types counted them once completed, in any epoch: the most of them.
/// </summary>
/// <remarks>
/// A pass lists a unit's pages only when it reaches it, and goes from the newest patch to the
/// oldest: its first units are its biggest (some 1,140 pages against some 500 for an old
/// patch), so the mean of the listed ones overstates the rest. One aggregate per read, over
/// the passes picked in memory from their scope (a few hundred rows).
/// </remarks>
internal sealed class ExpectedPages(IDbContextFactory<LoDbDbContext> contexts)
{
    /// <summary>The pages of a unit by version, for the versions some unit counted.</summary>
    /// <param name="typeMask">The types of page of the unit (<see cref="TypeMask"/>).</param>
    /// <param name="versions">The versions wanted; every one when null.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    public async Task<IReadOnlyDictionary<string, int>> ReadAsync(
        int typeMask,
        IReadOnlyCollection<string>? versions,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var scopes = await db.PrerenderPasses.AsNoTracking()
            .Select(static pass => new { pass.Id, pass.Scope })
            .ToListAsync(cancellationToken);
        var passIds = scopes
            .Where(pass => TypeMask.Of(PassScopeJson.Read(pass.Scope)) == typeMask)
            .Select(static pass => pass.Id)
            .ToList();
        var units = db.PrerenderUnits.AsNoTracking()
            .Where(unit => passIds.Contains(unit.PassId)
                && unit.State == PrerenderUnitState.Completed);
        if (versions is not null)
        {
            var wanted = versions.ToList();
            units = units.Where(unit => wanted.Contains(unit.Version));
        }

        return await units.GroupBy(static unit => unit.Version)
            .Select(static version => new
            {
                version.Key,
                Pages = version.Max(static unit => unit.PagesTotal),
            })
            .ToDictionaryAsync(
                static version => version.Key,
                static version => version.Pages,
                StringComparer.Ordinal,
                cancellationToken);
    }
}
