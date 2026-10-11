using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>
/// Least privilege for the API: <c>migrate</c>, which connects as the server's superuser,
/// keeps the roles of <see cref="DatabaseRoleNames"/> in place after the migrations, then
/// hands the schema's objects to their owners and grants the API its rows.
/// </summary>
/// <remarks>
/// <para>
/// The API connects as <see cref="DatabaseRoleNames.App"/>: it reads and writes rows, and its
/// only DDL, the daily partitions of the analytics, goes through two functions that run as
/// their owner. It cannot create, alter or drop anything else, and the server cancels any of
/// its statements past <see cref="AppRoleOptions.StatementTimeout"/>.
/// </para>
/// <para>
/// The superuser itself is left as it is: the legacy stack still connects as it until the
/// contract, and a rollback to it needs no change.
/// </para>
/// </remarks>
public sealed partial class DatabaseRoles(
    IOptions<AppRoleOptions> options,
    ILogger<DatabaseRoles> logger)
{
    /// <summary>The roles, then the privileges of the schema, in one transaction.</summary>
    /// <exception cref="InvalidOperationException">The session is not a superuser's.</exception>
    public async Task<RoleChanges> ApplyAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var changes = await ClusterRoles.EnsureAsync(
            transaction,
            options.Value,
            cancellationToken);
        await SchemaPrivileges.ApplyAsync(transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogApplied(logger, changes.Created, changes.Altered, changes.PasswordSet);
        return changes;
    }

    /// <summary>
    /// The roles alone, which belong to the server: what a test server prepares once for
    /// every database it then migrates.
    /// </summary>
    /// <exception cref="InvalidOperationException">The session is not a superuser's.</exception>
    public async Task<RoleChanges> EnsureRolesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var changes = await ClusterRoles.EnsureAsync(
            transaction,
            options.Value,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changes;
    }

    [LoggerMessage(
        EventName = "db.roles.applied",
        Level = LogLevel.Information,
        Message = "Database roles in place: {Created} created, {Altered} altered, password"
            + " changed: {PasswordChanged}; schema owners and grants applied.")]
    private static partial void LogApplied(
        ILogger logger,
        int created,
        int altered,
        bool passwordChanged);
}
