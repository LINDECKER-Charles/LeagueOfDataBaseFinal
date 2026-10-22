using LoDb.Domain.States.Fingerprints;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Ingestion.Catalog.States.Snapshots;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Ingestion.Catalog.States.Loading;

/// <summary>
/// Reads the stamp of the current generation, one row: what a process compares to its copy of
/// the index to know whether a rebuild or an extension made it stale.
/// </summary>
internal sealed class StateIndexStamps(IDbContextFactory<LoDbDbContext> contexts)
{
    /// <returns>The stamp; null while no generation of the compiled scheme is current.</returns>
    /// <exception cref="System.Data.Common.DbException">
    /// The database could not answer, the exception maybe wrapped by EF.
    /// </exception>
    public async Task<StateIndexStamp?> ReadAsync(CancellationToken cancellationToken)
    {
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var current = await db.CatalogStateGenerations.AsNoTracking()
                .Where(static generation =>
                    generation.Scheme == FingerprintScheme.Current && generation.IsCurrent)
                .Select(static generation => new { generation.Id, generation.LastVersion })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            return current is null
                ? null
                : new StateIndexStamp(
                    current.Id,
                    current.LastVersion is { } end ? PatchVersion.Parse(end) : null);
        }
    }
}
