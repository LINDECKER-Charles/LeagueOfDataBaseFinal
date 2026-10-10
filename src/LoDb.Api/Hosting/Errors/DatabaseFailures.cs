using System.Collections.Frozen;
using Npgsql;

namespace LoDb.Api.Hosting.Errors;

/// <summary>
/// Tells, from what a request threw, whether the database failed it and how; anything else
/// is a fault of the code.
/// </summary>
internal static class DatabaseFailures
{
    // Npgsql gives its pool timeout neither a type nor a code of its own: the message is its
    // only mark. DatabaseFailureTests exhausts a real pool, so a release that rewords it fails
    // the build instead of turning shed load into an outage.
    private const string PoolExhaustedPrefix = "The connection pool has been exhausted";

    // The server answered, but gives this API no working session.
    private static readonly FrozenSet<string> UnavailableStates = new[]
    {
        PostgresErrorCodes.ConnectionException,
        PostgresErrorCodes.ConnectionDoesNotExist,
        PostgresErrorCodes.ConnectionFailure,
        PostgresErrorCodes.SqlClientUnableToEstablishSqlConnection,
        PostgresErrorCodes.SqlServerRejectedEstablishmentOfSqlConnection,
        PostgresErrorCodes.InvalidAuthorizationSpecification,
        PostgresErrorCodes.InvalidPassword,
        PostgresErrorCodes.InvalidCatalogName,
        PostgresErrorCodes.TooManyConnections,
        PostgresErrorCodes.AdminShutdown,
        PostgresErrorCodes.CrashShutdown,
        PostgresErrorCodes.CannotConnectNow,
        PostgresErrorCodes.DatabaseDropped,
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <returns>The failure, or null when the database is not the cause.</returns>
    public static DatabaseFailure? Of(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // EF wraps the provider's exception (DbUpdateException, the InvalidOperationException
        // of a transient failure, RetryLimitExceededException): the cause lies further in.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException npgsql)
            {
                return Of(npgsql);
            }
        }

        return null;
    }

    private static DatabaseFailure? Of(NpgsqlException exception) => exception switch
    {
        PostgresException server => UnavailableStates.Contains(server.SqlState)
            ? DatabaseFailure.Unavailable
            : null,
        { InnerException: TimeoutException } when IsPoolTimeout(exception) =>
            DatabaseFailure.PoolExhausted,

        // No answer from the server: a refused or lost connection, a failed name lookup, a
        // connection or a command that timed out.
        { IsTransient: true } => DatabaseFailure.Unavailable,
        _ => null,
    };

    private static bool IsPoolTimeout(NpgsqlException exception) =>
        exception.Message.StartsWith(PoolExhaustedPrefix, StringComparison.Ordinal);
}
