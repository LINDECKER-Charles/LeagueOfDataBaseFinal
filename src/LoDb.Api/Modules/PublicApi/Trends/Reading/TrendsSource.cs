using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.PublicApi.Trends.Reading;

/// <summary>
/// The entity counters of the daily aggregates (<c>analytics_daily</c>), which go-api read
/// from the files of the storage volume.
/// </summary>
internal sealed class TrendsSource(IDbContextFactory<LoDbDbContext> contexts)
{
    /// <summary>
    /// The <c>entities</c> map of each day aggregated from <paramref name="from"/> to
    /// <paramref name="to"/>, as JSON text; null for a day without one.
    /// </summary>
    public async Task<IReadOnlyList<string?>> ReadEntitiesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.Database
            .SqlQuery<string?>(
                $"""
                SELECT (buckets -> 'entities')::text AS "Value"
                  FROM analytics_daily
                 WHERE day BETWEEN {from} AND {to}
                """)
            .ToListAsync(cancellationToken);
    }
}
