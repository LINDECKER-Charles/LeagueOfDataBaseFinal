using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LoDb.Infrastructure.Persistence;

/// <summary>
/// The pooled data source of <c>ConnectionStrings:LoDb</c>, and the options every
/// <see cref="LoDbDbContext"/> is built with.
/// </summary>
public static class LoDbDataSource
{
    /// <summary>Name under <c>ConnectionStrings</c>, the one the readiness check reads.</summary>
    public const string ConnectionStringName = "LoDb";

    /// <summary>Reads the connection string when first resolved, never at registration.</summary>
    /// <remarks>
    /// No logger factory: Npgsql would log every command at Information. EF logs the
    /// failed ones, and the traces carry the rest.
    /// </remarks>
    public static NpgsqlDataSource Create(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var builder = new NpgsqlDataSourceBuilder(ConnectionString(configuration))
        {
            Name = "lodb",
        };
        return builder.Build();
    }

    /// <summary>The connection string, or an error that names the missing setting.</summary>
    public static string ConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is not set.")
            : connectionString;
    }

    /// <summary>Provider, naming and log levels shared by the host and the design time.</summary>
    public static DbContextOptionsBuilder UseLoDb(
        this DbContextOptionsBuilder options,
        NpgsqlDataSource dataSource) =>
        options
            .UseNpgsql(dataSource)
            .UseSnakeCaseNamingConvention()
            // One line per executed command would drown the logs: they stay at Debug.
            .ConfigureWarnings(static warnings => warnings.Log(
                (RelationalEventId.CommandExecuted, LogLevel.Debug),
                (CoreEventId.ContextInitialized, LogLevel.Debug)));
}
