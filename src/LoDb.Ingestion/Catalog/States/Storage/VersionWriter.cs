using LoDb.Domain.Paths;
using LoDb.Domain.States;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.States;
using LoDb.Ingestion.Catalog.States.Building;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LoDb.Ingestion.Catalog.States.Storage;

/// <summary>
/// Writes what indexing a version changes, in one transaction: the states it closes, those it
/// opens, the version's place in the coverage, and the generation's end and counters.
/// </summary>
/// <remarks>
/// A version is written whole or not at all: a walk that stops resumes after the last version
/// written. The closures go first, in one statement: an entity's new state may only open once
/// its previous one is closed (one open state per entity). Each statement stays far under the
/// API role's 15 s: a version opens a thousand states at most, its first time.
/// </remarks>
internal sealed class VersionWriter(
    IDbContextFactory<LoDbDbContext> contexts,
    TimeProvider timeProvider)
{
    private const string CloseSql = """
        UPDATE catalog_entity_state AS s
        SET last_version = @last, closed_at = c.closed_at
        FROM unnest(@resources, @keys, @closed) AS c(resource, entity_key, closed_at)
        WHERE s.generation_id = @generation AND s.closed_at IS NULL
            AND s.resource = c.resource AND s.entity_key = c.entity_key
        """;

    public async Task WriteAsync(
        GenerationCursor cursor,
        VersionChanges changes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        ArgumentNullException.ThrowIfNull(changes);
        var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (db.ConfigureAwait(false))
        {
            var transaction = await db.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await CloseAsync(db, cursor, changes.Closed, cancellationToken)
                    .ConfigureAwait(false);
                await OpenAsync(db, cursor, changes, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task CloseAsync(
        LoDbDbContext db,
        GenerationCursor cursor,
        IReadOnlyList<ClosedState> closed,
        CancellationToken cancellationToken)
    {
        if (closed.Count == 0)
        {
            return;
        }

        var written = await db.Database
            .ExecuteSqlRawAsync(CloseSql, Parameters(cursor, closed), cancellationToken)
            .ConfigureAwait(false);
        if (written != closed.Count)
        {
            throw new InvalidOperationException(
                $"{closed.Count} states of generation {cursor.GenerationId} were to close,"
                + $" {written} were open.");
        }
    }

    private async Task OpenAsync(
        LoDbDbContext db,
        GenerationCursor cursor,
        VersionChanges changes,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        db.CatalogEntityStates.AddRange(changes.Opened.Select(state => RowOf(cursor, state)));
        db.CatalogStateVersions.Add(new CatalogStateVersion
        {
            GenerationId = cursor.GenerationId,
            Version = changes.Version.Value,
            Ordinal = cursor.NextOrdinal,
            IndexedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var opened = changes.Opened.Count;
        var stillOpen = opened - changes.Closed.Count;
        await db.CatalogStateGenerations
            .Where(generation => generation.Id == cursor.GenerationId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(generation => generation.LastVersion, changes.Version.Value)
                    .SetProperty(generation => generation.UpdatedAt, now)
                    .SetProperty(
                        generation => generation.StateCount,
                        generation => generation.StateCount + opened)
                    .SetProperty(
                        generation => generation.OpenStateCount,
                        generation => generation.OpenStateCount + stillOpen),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // The states a version closes all end at the covered version before it.
    private static NpgsqlParameter[] Parameters(
        GenerationCursor cursor,
        IReadOnlyList<ClosedState> closed) =>
        [
            new("generation", cursor.GenerationId),
            new("last", closed[0].Last.Value),
            new("resources", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = closed.Select(static state => CanonicalPath.SegmentOf(state.Resource))
                    .ToArray(),
            },
            new("keys", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = closed.Select(static state => state.Key).ToArray(),
            },
            new("closed", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz)
            {
                Value = closed.Select(cursor.Closing.Of).ToArray(),
            },
        ];

    private static CatalogEntityState RowOf(GenerationCursor cursor, OpenedState state) => new()
    {
        GenerationId = cursor.GenerationId,
        Resource = state.Resource,
        EntityKey = state.Key,
        OpenerVersion = state.Opener.Value,
        Fingerprint = state.Fingerprint,
        Path = state.Path,
        PreviousOpenerVersion = state.PreviousOpener?.Value,
        IsReturn = state.Kind == EntityChangeKind.Returned,
        ChangedFields = state.ChangedFields.Select(static key => key.ToDotted()).ToArray(),
    };
}
