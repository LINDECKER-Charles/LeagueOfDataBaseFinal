using System.Globalization;
using Npgsql;

namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>
/// The roles of <see cref="DatabaseRoleNames"/> on the server: created when missing, their
/// attributes, the API's statement timeout and password set when they differ.
/// </summary>
/// <remarks>
/// Roles belong to the server, not to a database. They are read first and only what differs
/// is written: a run that finds them in place writes nothing, and runs on two databases of
/// one server (the tests') never update the same role at once. No role ever gets an
/// attribute that would let it step outside its grants (superuser, creating roles or
/// databases, replication, bypassing row security).
/// </remarks>
internal static class ClusterRoles
{
    private const string Create = "CREATE";
    private const string Alter = "ALTER";
    private const string Login = "LOGIN";
    private const string NoLogin = "NOLOGIN";
    private const string Restricted =
        "NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS";

    private const string StatementTimeout = "statement_timeout";
    private const string Milliseconds = "ms";
    private const string NamesParameter = "names";

    private const string SuperuserQuery =
        "SELECT rolsuper FROM pg_roles WHERE rolname = current_user";

    // The role-wide settings (database 0), as ALTER ROLE … SET writes them: name=value.
    private const string StateQuery = """
        SELECT a.rolname, a.rolcanlogin,
            a.rolsuper OR a.rolcreatedb OR a.rolcreaterole OR a.rolreplication
                OR a.rolbypassrls,
            a.rolconnlimit, a.rolpassword, coalesce(s.setconfig, '{}')
        FROM pg_authid a
        LEFT JOIN pg_db_role_setting s ON s.setrole = a.oid AND s.setdatabase = 0
        WHERE a.rolname = ANY(@names)
        """;

    private static readonly string[] Names =
        [DatabaseRoleNames.Owner, DatabaseRoleNames.Partitions, DatabaseRoleNames.App];

    /// <summary>Puts the roles as <paramref name="options"/> describes them.</summary>
    /// <exception cref="InvalidOperationException">The session is not a superuser's.</exception>
    public static async Task<RoleChanges> EnsureAsync(
        NpgsqlTransaction transaction,
        AppRoleOptions options,
        CancellationToken cancellationToken)
    {
        if (!await IsSuperuserAsync(transaction, cancellationToken))
        {
            throw new InvalidOperationException(
                "The database roles are kept by migrate, which must connect as a superuser.");
        }

        var run = new Run(transaction, options, await ReadAsync(transaction, cancellationToken));
        var (created, altered) = await EnsureAttributesAsync(run, cancellationToken);
        if (await EnsureStatementTimeoutAsync(run, cancellationToken)
            && run.States.ContainsKey(DatabaseRoleNames.App))
        {
            altered.Add(DatabaseRoleNames.App);
        }

        var passwordSet = await EnsurePasswordAsync(run, cancellationToken);
        return new RoleChanges(created, altered.Count, passwordSet);
    }

    private static RoleSpec[] Specs(AppRoleOptions options) =>
    [
        new(DatabaseRoleNames.Owner, CanLogin: false, AppRoleOptions.UnlimitedConnections),
        new(DatabaseRoleNames.Partitions, CanLogin: false, AppRoleOptions.UnlimitedConnections),
        new(DatabaseRoleNames.App, CanLogin: true, options.ConnectionLimit),
    ];

    // The roles created, and the names of the existing ones whose attributes changed.
    private static async Task<(int Created, HashSet<string> Altered)> EnsureAttributesAsync(
        Run run,
        CancellationToken cancellationToken)
    {
        var created = 0;
        var altered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in Specs(run.Options))
        {
            var state = run.States.GetValueOrDefault(spec.Name);
            if (state is null)
            {
                await ExecuteAsync(run, spec.Statement(Create), cancellationToken);
                created++;
            }
            else if (!spec.IsMetBy(state))
            {
                await ExecuteAsync(run, spec.Statement(Alter), cancellationToken);
                altered.Add(spec.Name);
            }
        }

        return (created, altered);
    }

    private static async Task<bool> EnsureStatementTimeoutAsync(
        Run run,
        CancellationToken cancellationToken)
    {
        var value = string.Create(
            CultureInfo.InvariantCulture,
            $"{(long)run.Options.StatementTimeout.TotalMilliseconds}{Milliseconds}");
        var settings = run.States.GetValueOrDefault(DatabaseRoleNames.App)?.Settings ?? [];
        if (settings.Contains($"{StatementTimeout}={value}", StringComparer.Ordinal))
        {
            return false;
        }

        var sql = $"ALTER ROLE {DatabaseRoleNames.App} SET {StatementTimeout} = '{value}'";
        await ExecuteAsync(run, sql, cancellationToken);
        return true;
    }

    // The verifier holds base64, digits, '$' and ':' only: it needs no escaping in a literal.
    private static async Task<bool> EnsurePasswordAsync(
        Run run,
        CancellationToken cancellationToken)
    {
        var password = run.Options.Password;
        var stored = run.States.GetValueOrDefault(DatabaseRoleNames.App)?.Password;
        if (string.IsNullOrEmpty(password) || ScramVerifier.Matches(password, stored))
        {
            return false;
        }

        var verifier = ScramVerifier.Create(password);
        var sql = $"ALTER ROLE {DatabaseRoleNames.App} PASSWORD '{verifier}'";
        await ExecuteAsync(run, sql, cancellationToken);
        return true;
    }

    private static async Task<bool> IsSuperuserAsync(
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            SuperuserQuery,
            transaction.Connection,
            transaction);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<IReadOnlyDictionary<string, RoleState>> ReadAsync(
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            StateQuery,
            transaction.Connection,
            transaction);
        command.Parameters.AddWithValue(NamesParameter, Names);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var states = new Dictionary<string, RoleState>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            var state = new RoleState(
                CanLogin: reader.GetBoolean(1),
                IsPrivileged: reader.GetBoolean(2),
                ConnectionLimit: reader.GetInt32(3),
                Password: reader.IsDBNull(4) ? null : reader.GetString(4),
                Settings: reader.GetFieldValue<string[]>(5));
            states.Add(reader.GetString(0), state);
        }

        return states;
    }

    private static async Task ExecuteAsync(
        Run run,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            sql,
            run.Transaction.Connection,
            run.Transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record Run(
        NpgsqlTransaction Transaction,
        AppRoleOptions Options,
        IReadOnlyDictionary<string, RoleState> States);

    private sealed record RoleState(
        bool CanLogin,
        bool IsPrivileged,
        int ConnectionLimit,
        string? Password,
        string[] Settings);

    private sealed record RoleSpec(string Name, bool CanLogin, int ConnectionLimit)
    {
        /// <summary><c>CREATE ROLE</c> or <c>ALTER ROLE</c> with every attribute.</summary>
        public string Statement(string verb)
        {
            var login = CanLogin ? Login : NoLogin;
            var limit = ConnectionLimit.ToString(CultureInfo.InvariantCulture);
            return $"{verb} ROLE {Name} {login} {Restricted} CONNECTION LIMIT {limit}";
        }

        public bool IsMetBy(RoleState state) =>
            state.CanLogin == CanLogin
            && !state.IsPrivileged
            && state.ConnectionLimit == ConnectionLimit;
    }
}
