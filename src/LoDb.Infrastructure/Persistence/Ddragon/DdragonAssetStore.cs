using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace LoDb.Infrastructure.Persistence.Ddragon;

/// <summary>
/// <see cref="IDdragonAssetStore"/> on PostgreSQL: one upsert per batch, rows written in key
/// order so that two overlapping batches never deadlock.
/// </summary>
internal sealed class DdragonAssetStore(
    NpgsqlDataSource dataSource,
    IDbContextFactory<LoDbDbContext> contexts,
    TimeProvider timeProvider) : IDdragonAssetStore
{
    private const string Insert = """
        INSERT INTO ddragon_asset (version, type, key, status, sha256, extension, recorded_at)
        SELECT version, type, key, status, sha256, extension, @recorded_at
        FROM unnest(@versions, @types, @keys, @statuses, @sha256s, @extensions)
            AS entry (version, type, key, status, sha256, extension)
        ORDER BY version, type, key
        ON CONFLICT (version, type, key) DO
        """;

    private const string KeepRecorded = " NOTHING";

    private const string ReplaceChanged = """
         UPDATE SET
            status = excluded.status,
            sha256 = excluded.sha256,
            extension = excluded.extension,
            recorded_at = excluded.recorded_at
        WHERE (ddragon_asset.status, ddragon_asset.sha256, ddragon_asset.extension)
            IS DISTINCT FROM (excluded.status, excluded.sha256, excluded.extension)
        """;

    public async Task<int> RecordAsync(
        IReadOnlyCollection<DdragonAssetEntry> entries,
        bool force,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var batch = entries
            .GroupBy(static entry => (entry.Version, entry.Type, entry.Key))
            .Select(static group => group.Last())
            .ToList();
        if (batch.Count == 0)
        {
            return 0;
        }

        await using var command = dataSource.CreateCommand(
            Insert + (force ? ReplaceChanged : KeepRecorded));
        command.Parameters.AddWithValue("recorded_at", timeProvider.GetUtcNow());
        AddArray(command, "versions", batch.Select(static entry => entry.Version));
        AddArray(command, "types", batch.Select(static entry => entry.Type));
        AddArray(command, "keys", batch.Select(static entry => entry.Key));
        AddArray(
            command,
            "statuses",
            batch.Select(static entry => DdragonColumns.ToText(entry.Status)));
        AddArray(command, "sha256s", batch.Select(static entry => entry.Sha256));
        AddArray(command, "extensions", batch.Select(static entry => entry.Extension));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DdragonAsset>> ListAsync(
        string version,
        string type,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.DdragonAssets
            .AsNoTracking()
            .Where(asset => asset.Version == version && asset.Type == type)
            .OrderBy(static asset => asset.Key)
            .ToListAsync(cancellationToken);
    }

    private static void AddArray(NpgsqlCommand command, string name, IEnumerable<string?> values)
    {
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = values.ToArray(),
        });
    }
}
