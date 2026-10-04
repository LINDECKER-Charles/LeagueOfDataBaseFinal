using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Infrastructure.Persistence.Ddragon;

internal sealed class DdragonVersionStore(
    NpgsqlDataSource dataSource,
    IDbContextFactory<LoDbDbContext> contexts,
    TimeProvider timeProvider) : IDdragonVersionStore
{
    private const string Discover = """
        INSERT INTO ddragon_version (version, status, attempts, discovered_at, updated_at)
        VALUES (@version, 'discovered', 0, @now, @now)
        ON CONFLICT (version) DO NOTHING
        """;

    public async Task<bool> DiscoverAsync(string version, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(Discover);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<IReadOnlyList<DdragonVersion>> ListAsync(
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await db.DdragonVersions
            .AsNoTracking()
            .OrderBy(static version => version.Version)
            .ToListAsync(cancellationToken);
    }
}
