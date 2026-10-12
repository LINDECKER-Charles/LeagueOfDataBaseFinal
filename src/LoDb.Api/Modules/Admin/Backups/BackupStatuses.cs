namespace LoDb.Api.Modules.Admin.Backups;

/// <summary>
/// The words of a database's backups or restore tests: what the last attempt says, against
/// the deadlines of infra-vps's alerts.
/// </summary>
internal static class BackupStatuses
{
    /// <summary>The last attempt succeeded, within its deadline.</summary>
    public const string Ok = "ok";

    /// <summary>The last attempt succeeded, but longer ago than its deadline.</summary>
    public const string Late = "late";

    /// <summary>The last attempt failed.</summary>
    public const string Failed = "failed";

    /// <summary>
    /// A night's pass, plus two hours of hourly retries: <c>SauvegardeEnRetard</c> rings past
    /// it (infra-vps, observability/vmalert/rules/20-hote.yml).
    /// </summary>
    public static readonly TimeSpan BackupDeadline = TimeSpan.FromHours(26);

    /// <summary>
    /// A month, plus a week of margin: <c>RestaurationNonVerifiee</c> rings past it.
    /// </summary>
    public static readonly TimeSpan RestoreDeadline = TimeSpan.FromDays(35);

    /// <summary>
    /// The word for a database's attempts, given its last success and its last failure: a
    /// success counts while it is no older than <paramref name="freshSince"/>.
    /// </summary>
    public static string Of(
        DateTimeOffset? lastSuccess,
        DateTimeOffset? lastFailure,
        DateTimeOffset freshSince)
    {
        if (lastFailure is { } failure && (lastSuccess is not { } success || failure > success))
        {
            return Failed;
        }

        return lastSuccess >= freshSince ? Ok : Late;
    }
}
