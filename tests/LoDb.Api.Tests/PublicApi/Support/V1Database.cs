using System.Globalization;
using LoDb.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoDb.Api.Tests.PublicApi.Support;

/// <summary>
/// A copy of the template database of a <see cref="V1Server"/>, for one run, dropped with it.
/// </summary>
/// <remarks>
/// The tests reach it without a pool: a connection of theirs never outlives an outage they
/// cause.
/// </remarks>
public sealed class V1Database : IAsyncDisposable
{
    // The template refuses to be copied while a connection to it lingers.
    private const string ObjectInUse = "55006";
    private const int CopyAttempts = 50;
    private static readonly TimeSpan CopyRetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly string _server;

    private V1Database(string server, string name)
    {
        _server = server;
        Name = name;
        ConnectionString = new NpgsqlConnectionStringBuilder(server)
        {
            Database = name,
        }.ConnectionString;
        DataSource = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Pooling = false,
        });
    }

    public string Name { get; }

    /// <summary>The connection string the API is given.</summary>
    public string ConnectionString { get; }

    public NpgsqlDataSource DataSource { get; }

    /// <summary>A context on the copy, configured as the API configures it.</summary>
    public LoDbDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LoDbDbContext>();
        options.UseLoDb(DataSource);
        return new LoDbDbContext(options.Options);
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var command = DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(V1Server.Token);
    }

    /// <summary>The first column of the first row, as text; null for none or NULL.</summary>
    public async Task<string?> ScalarAsync(string sql)
    {
        await using var command = DataSource.CreateCommand(sql);
        var value = await command.ExecuteScalarAsync(V1Server.Token);
        return value is null or DBNull
            ? null
            : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    /// <summary>Refuses every new connection and ends the open ones: PostgreSQL is down.</summary>
    public Task StopAsync() => OnServerAsync(
        _server,
        $"ALTER DATABASE {Name} ALLOW_CONNECTIONS false;"
        + " SELECT pg_terminate_backend(pid) FROM pg_stat_activity"
        + $" WHERE datname = '{Name}' AND pid <> pg_backend_pid()");

    /// <summary>Accepts connections again.</summary>
    public Task StartAsync() =>
        OnServerAsync(_server, $"ALTER DATABASE {Name} ALLOW_CONNECTIONS true");

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await OnServerAsync(_server, $"DROP DATABASE IF EXISTS {Name} WITH (FORCE)");
    }

    internal static async Task<V1Database> CopyAsync(string server, string template)
    {
        var name = "v1_" + Guid.NewGuid().ToString("N");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await OnServerAsync(server, $"CREATE DATABASE {name} TEMPLATE {template}");
                return new V1Database(server, name);
            }
            catch (PostgresException exception) when (
                exception.SqlState == ObjectInUse && attempt < CopyAttempts)
            {
                await Task.Delay(CopyRetryDelay, V1Server.Token);
            }
        }
    }

    private static async Task OnServerAsync(string server, string sql)
    {
        await using var connection = new NpgsqlConnection(server);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
