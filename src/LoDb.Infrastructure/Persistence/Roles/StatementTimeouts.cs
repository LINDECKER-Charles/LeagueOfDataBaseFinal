using System.Globalization;
using Npgsql;

namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>
/// Raises, for one transaction, the statement timeout the API's role gets from
/// <see cref="AppRoleOptions.StatementTimeout"/>: for a job whose statements grow with the
/// traffic, such as reading a whole day of page views.
/// </summary>
public static class StatementTimeouts
{
    private const string Sql = "SELECT set_config('statement_timeout', @value, true)";
    private const string ValueParameter = "value";
    private const string Milliseconds = "ms";

    /// <summary>
    /// Sets <paramref name="timeout"/> until the end of <paramref name="transaction"/>, and
    /// the client's wait on <paramref name="command"/> to the same.
    /// </summary>
    public static async Task RaiseAsync(
        NpgsqlTransaction transaction,
        NpgsqlCommand command,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(command);
        await using var raise = new NpgsqlCommand(Sql, transaction.Connection, transaction);
        raise.Parameters.AddWithValue(
            ValueParameter,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{(long)timeout.TotalMilliseconds}{Milliseconds}"));
        await raise.ExecuteNonQueryAsync(cancellationToken);
        command.Transaction = transaction;
        command.CommandTimeout = (int)Math.Ceiling(timeout.TotalSeconds);
    }
}
