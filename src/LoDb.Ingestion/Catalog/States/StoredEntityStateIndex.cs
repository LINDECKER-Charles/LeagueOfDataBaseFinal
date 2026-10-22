using System.Data;
using LoDb.Domain.States.Fingerprints;
using LoDb.Domain.States.Fields;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.States;
using LoDb.Ingestion.Catalog.States.Loading;
using LoDb.Ingestion.Catalog.States.Snapshots;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Ingestion.Catalog.States;

/// <summary>
/// Reads the current generation of the compiled fingerprint scheme from the database, in one
/// snapshot transaction, so that a version being indexed shows whole or not at all.
/// </summary>
/// <remarks>
/// Reads at every call: the copy held per process, its stamp check and its invalidation after
/// a local write are the reload policy's.
/// </remarks>
internal sealed class StoredEntityStateIndex(IDbContextFactory<LoDbDbContext> contexts)
    : IEntityStateIndex
{
    public async ValueTask<EntityStateSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (UnreachableDatabase.IsCause(exception))
        {
            throw new StateIndexUnavailableException(
                "The index of states could not be read.",
                exception);
        }
    }

    private async Task<EntityStateSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var transaction = await db.Database
                .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                return await ReadAsync(db, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<EntityStateSnapshot> ReadAsync(
        LoDbDbContext db,
        CancellationToken cancellationToken)
    {
        var current = await db.CatalogStateGenerations.AsNoTracking()
            .Where(static generation =>
                generation.Scheme == FingerprintScheme.Current && generation.IsCurrent)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (current is null)
        {
            return EntityStateSnapshot.Empty;
        }

        var versions = await db.CatalogStateVersions.AsNoTracking()
            .Where(version => version.GenerationId == current.Id)
            .OrderBy(static version => version.Ordinal)
            .Select(static version => version.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var states = await db.CatalogEntityStates.AsNoTracking()
            .Where(state => state.GenerationId == current.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var promoted = await PromotionsAsync(db, cancellationToken).ConfigureAwait(false);
        return EntityStateSnapshot.Of(
            GenerationOf(current, versions),
            [.. states.Select(StateOf)],
            promoted);
    }

    private static async Task<IReadOnlyList<PromotedVersion>> PromotionsAsync(
        LoDbDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.DdragonVersions.AsNoTracking()
            .Where(static version => version.PromotedAt != null)
            .Select(static version => new { version.Version, version.PromotedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return
        [
            .. rows
                .Where(static row => PatchVersion.TryParse(row.Version, out _))
                .Select(static row => new PromotedVersion(
                    PatchVersion.Parse(row.Version),
                    row.PromotedAt!.Value)),
        ];
    }

    private static StateIndexGeneration GenerationOf(
        CatalogStateGeneration row,
        IEnumerable<string> versions) => new()
    {
        Id = row.Id,
        Scheme = row.Scheme,
        BuiltAt = row.BuiltAt,
        Versions = [.. versions.Select(PatchVersion.Parse)],
    };

    private static IndexedState StateOf(CatalogEntityState row) => new()
    {
        Resource = row.Resource,
        Key = row.EntityKey,
        Opener = PatchVersion.Parse(row.OpenerVersion),
        Last = row.LastVersion is { } last ? PatchVersion.Parse(last) : null,
        ClosedAt = row.ClosedAt,
        Fingerprint = row.Fingerprint,
        Path = row.Path,
        PreviousOpener = row.PreviousOpenerVersion is { } previous
            ? PatchVersion.Parse(previous)
            : null,
        IsReturn = row.IsReturn,
        ChangedFields = [.. row.ChangedFields.Select(FieldKey.Parse)],
    };
}
