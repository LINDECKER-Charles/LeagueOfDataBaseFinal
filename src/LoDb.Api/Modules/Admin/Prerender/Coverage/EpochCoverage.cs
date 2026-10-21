using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Prerender;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.Admin.Prerender.Coverage;

/// <summary>
/// What an epoch holds of the older patches, (version, locale) by (version, locale), read from
/// the units of its passes: the store cannot be listed (the writer key does not list).
/// </summary>
/// <remarks>
/// Two reads on one connection: the epoch's passes, a few hundred rows at most, then their
/// units that started or wrote something, by the prefix of the units' key (some 8,400 rows for
/// a whole pass).
/// </remarks>
internal sealed class EpochCoverage(IDbContextFactory<LoDbDbContext> contexts)
{
    /// <summary>The cells of <paramref name="epoch"/>, keyed by version and locale code.</summary>
    public async Task<CoverageMap> ReadAsync(int epoch, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var passes = await db.PrerenderPasses.AsNoTracking()
            .Where(pass => pass.Epoch == epoch)
            .Select(static pass => new { pass.Id, pass.Scope })
            .ToListAsync(cancellationToken);
        if (passes.Count == 0)
        {
            return CoverageMap.Empty;
        }

        var typesByPass = passes.ToDictionary(
            static pass => pass.Id,
            static pass => TypeMask.Of(PassScopeJson.Read(pass.Scope)));
        var passIds = typesByPass.Keys.ToList();
        var units = await db.PrerenderUnits.AsNoTracking()
            .Where(unit => passIds.Contains(unit.PassId)
                && (unit.State != PrerenderUnitState.Pending || unit.PagesWritten > 0))
            .Select(static unit => new CoverageUnit
            {
                PassId = unit.PassId,
                Version = unit.Version,
                Locale = unit.Locale,
                State = unit.State,
                PagesWritten = unit.PagesWritten,
                PagesFailed = unit.PagesFailed,
                BytesWritten = unit.BytesWritten,
                UpdatedAt = unit.UpdatedAt,
                EndedAt = unit.EndedAt,
            })
            .ToListAsync(cancellationToken);
        return CoverageFold.Of(units, typesByPass);
    }
}
