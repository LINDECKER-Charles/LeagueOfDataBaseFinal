using System.Globalization;
using Npgsql;

namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>
/// The roles of <see cref="DatabaseRoleNames"/> on the server: created when missing, their
/// attributes, the API's statement timeout and the passwords set when they differ.
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

    private const string AlterApp = $"ALTER ROLE {DatabaseRoleNames.App}";
    private const string ResetAll = "RESET ALL";
    private const string StatementTimeout = "statement_timeout";
    private const string Milliseconds = "ms";
    private const string NamesParameter = "names";

    private const string SuperuserQuery =
        "SELECT rolsuper FROM pg_roles WHERE rolname = current_user";

    // The role-wide settings (database 0), as ALTER ROLE … SET writes them: name=value; then
    // the databases, quoted, where ALTER ROLE … IN DATABASE gave the role settings of its own.
    private const string StateQuery = """
        SELECT a.rolname, a.rolcanlogin,
            a.rolsuper OR a.rolcreatedb OR a.rolcreaterole OR a.rolreplication
                OR a.rolbypassrls,
            a.rolconnlimit, a.rolpassword, coalesce(s.setconfig, '{}'),
            ARRAY(
                SELECT quote_ident(d.datname)
                FROM pg_db_role_setting o
                JOIN pg_database d ON d.oid = o.setdatabase
                WHERE o.setrole = a.oid AND o.setdatabase <> 0)
        FROM pg_authid a
        LEFT JOIN pg_db_role_setting s ON s.setrole = a.oid AND s.setdatabase = 0
        WHERE a.rolname = ANY(@names)
        """;

    private static readonly string[] Names =
    [
        DatabaseRoleNames.Owner,
        DatabaseRoleNames.Partitions,
        DatabaseRoleNames.App,
        DatabaseRoleNames.Backup,
    ];

    /// <summary>Puts the roles as <paramref name="settings"/> describes them.</summary>
    /// <exception cref="InvalidOperationException">The session is not a superuser's.</exception>
    public static async Task<RoleChanges> EnsureAsync(
        NpgsqlTransaction transaction,
        RoleSettings settings,
        CancellationToken cancellationToken)
    {
        if (!await IsSuperuserAsync(transaction, cancellationToken))
        {
            throw new InvalidOperationException(
                "The database roles are kept by migrate, which must connect as a superuser.");
        }

        var run = new Run(transaction, settings, await ReadAsync(transaction, cancellationToken));
        var (created, altered) = await EnsureAttributesAsync(run, cancellationToken);
        if (await EnsureSettingsAsync(run, cancellationToken)
            && run.States.ContainsKey(DatabaseRoleNames.App))
        {
            altered.Add(DatabaseRoleNames.App);
        }

        var passwordSet = await EnsurePasswordsAsync(run, cancellationToken);
        return new RoleChanges(created, altered.Count, passwordSet);
    }

    private static RoleSpec[] Specs(RoleSettings settings) =>
    [
        new(DatabaseRoleNames.Owner, CanLogin: false, AppRoleOptions.UnlimitedConnections),
        new(DatabaseRoleNames.Partitions, CanLogin: false, AppRoleOptions.UnlimitedConnections),
        new(DatabaseRoleNames.App, CanLogin: true, settings.App.ConnectionLimit),
        new(DatabaseRoleNames.Backup, CanLogin: true, BackupRoleOptions.ConnectionLimit),
    ];

    // The roles created, and the names of the existing ones whose attributes changed.
    private static async Task<(int Created, HashSet<string> Altered)> EnsureAttributesAsync(
        Run run,
        CancellationToken cancellationToken)
    {
        var created = 0;
        var altered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in Specs(run.Settings))
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

    // The API's sessions get the statement timeout and nothing else: a setting added by hand,
    // or by the role itself (a role may set its own), such as a timeout of 0 for one database,
    // which would prevail over the role-wide one, is reset.
    private static async Task<bool> EnsureSettingsAsync(
        Run run,
        CancellationToken cancellationToken)
    {
        var value = string.Create(
            CultureInfo.InvariantCulture,
            $"{(long)run.Settings.App.StatementTimeout.TotalMilliseconds}{Milliseconds}");
        var state = run.States.GetValueOrDefault(DatabaseRoleNames.App);
        var isRoleWideKept = state?.Settings is [var only] && only == $"{StatementTimeout}={value}";
        var statements = (state?.DatabasesWithSettings ?? [])
            .Select(static database => $"{AlterApp} IN DATABASE {database} {ResetAll}")
            .ToList();
        if (!isRoleWideKept)
        {
            statements.Add($"{AlterApp} {ResetAll}");
            statements.Add($"{AlterApp} SET {StatementTimeout} = '{value}'");
        }

        foreach (var statement in statements)
        {
            await ExecuteAsync(run, statement, cancellationToken);
        }

        return statements.Count > 0;
    }

    // Both passwords, each written only when it is set and differs: not short-circuited.
    private static async Task<bool> EnsurePasswordsAsync(
        Run run,
        CancellationToken cancellationToken)
    {
        var app = run.Settings.App.Password;
        var backup = run.Settings.Backup.Password;
        var isAppSet = await EnsurePasswordAsync(
            run,
            DatabaseRoleNames.App,
            app,
            cancellationToken);
        var isBackupSet = await EnsurePasswordAsync(
            run,
            DatabaseRoleNames.Backup,
            backup,
            cancellationToken);
        return isAppSet || isBackupSet;
    }

    // The verifier holds base64, digits, '$' and ':' only: it needs no escaping in a literal.
    // The role is one of the constant names of DatabaseRoleNames, never configured text.
    private static async Task<bool> EnsurePasswordAsync(
        Run run,
        string role,
        string? password,
        CancellationToken cancellationToken)
    {
        var stored = run.States.GetValueOrDefault(role)?.Password;
        if (string.IsNullOrEmpty(password) || ScramVerifier.Matches(password, stored))
        {
            return false;
        }

        var verifier = ScramVerifier.Create(password);
        var sql = $"ALTER ROLE {role} PASSWORD '{verifier}'";
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
                Settings: reader.GetFieldValue<string[]>(5),
                DatabasesWithSettings: reader.GetFieldValue<string[]>(6));
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
        RoleSettings Settings,
        IReadOnlyDictionary<string, RoleState> States);

    private sealed record RoleState(
        bool CanLogin,
        bool IsPrivileged,
        int ConnectionLimit,
        string? Password,
        string[] Settings,
        string[] DatabasesWithSettings);

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
