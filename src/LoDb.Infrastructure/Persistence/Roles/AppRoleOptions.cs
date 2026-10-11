namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>
/// The API's database role as <c>migrate</c> sets it (<c>LoDb:Database:AppRole</c>), checked
/// when the host starts.
/// </summary>
/// <remarks>
/// The API itself never reads these settings: it connects as <see cref="DatabaseRoleNames.App"/>
/// with the password of its own connection string, the one given here to <c>migrate</c>.
/// </remarks>
public sealed class AppRoleOptions
{
    public const string SectionName = "LoDb:Database:AppRole";

    /// <summary>A role connection limit of PostgreSQL that sets none.</summary>
    public const int UnlimitedConnections = -1;

    // The API's pool (32) plus the commands run in its image, with room to spare.
    private const int DefaultConnectionLimit = 60;
    private const int DefaultStatementTimeoutSeconds = 15;

    /// <summary>
    /// Password of the role, printable ASCII. Unset, <c>migrate</c> leaves the current one
    /// as it is, and a role it creates has none until a run sets it.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Longest statement of the API's sessions, past which the server cancels it. A job that
    /// legitimately reads more raises it for its own transaction.
    /// </summary>
    public TimeSpan StatementTimeout { get; set; } =
        TimeSpan.FromSeconds(DefaultStatementTimeoutSeconds);

    /// <summary>
    /// Sessions the role may hold at once, or <see cref="UnlimitedConnections"/>.
    /// </summary>
    public int ConnectionLimit { get; set; } = DefaultConnectionLimit;
}
