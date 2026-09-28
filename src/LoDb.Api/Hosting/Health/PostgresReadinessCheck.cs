using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace LoDb.Api.Hosting.Health;

/// <summary>
/// Readiness of the database: a <c>SELECT 1</c> on <c>ConnectionStrings:LoDb</c>.
/// </summary>
/// <remarks>
/// The check owns a data source created on its first run: registering it opens nothing, so
/// a host without a database (the OpenAPI generation, most tests) builds and starts.
/// </remarks>
internal sealed class PostgresReadinessCheck(IConfiguration configuration)
    : IHealthCheck, IAsyncDisposable
{
    public const string ConnectionStringName = "LoDb";
    private const string MissingConnectionString = "ConnectionStrings:LoDb is not set.";
    private const string Probe = "SELECT 1";

    private readonly Lazy<NpgsqlDataSource?> _dataSource = new(() => Create(configuration));

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_dataSource.Value is not { } dataSource)
        {
            return HealthCheckResult.Unhealthy(MissingConnectionString);
        }

        await using var command = dataSource.CreateCommand(Probe);
        await command.ExecuteScalarAsync(cancellationToken);
        return HealthCheckResult.Healthy();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource.IsValueCreated && _dataSource.Value is { } dataSource)
        {
            await dataSource.DisposeAsync();
        }
    }

    private static NpgsqlDataSource? Create(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        return string.IsNullOrWhiteSpace(connectionString)
            ? null
            : NpgsqlDataSource.Create(connectionString);
    }
}
