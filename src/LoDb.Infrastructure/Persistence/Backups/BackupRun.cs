namespace LoDb.Infrastructure.Persistence.Backups;

/// <summary>
/// A row of <c>backup_run</c>: one step of a pass of the host's off-host backups, as the
/// backup container of infra-vps reports it once the step is over, failed or not.
/// </summary>
/// <remarks>
/// The container writes these rows as <c>lodb_sauvegarde</c>, which may insert here and
/// nowhere else (<c>schema-privileges.sql</c>); the API only reads them. A step of one
/// database (<see cref="BackupStep.Backup"/>, <see cref="BackupStep.Restore"/>) names it, a
/// step of the whole repository names none.
/// </remarks>
public sealed class BackupRun
{
    /// <summary>The longest name of a database, PostgreSQL's identifier limit.</summary>
    public const int DatabaseMaxLength = 63;

    /// <summary>The longest reason or summary the container keeps of a step.</summary>
    public const int DetailMaxLength = 500;

    public long Id { get; set; }

    /// <summary>
    /// The database of the step as infra-vps names it (<c>lodb</c>, <c>umami</c>), in the
    /// column <c>base</c>.
    /// </summary>
    public string? Database { get; set; }

    public BackupStep Step { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public bool Succeeded { get; set; }

    /// <summary>A backup: the size of the dump restic read.</summary>
    public long? BytesRead { get; set; }

    /// <summary>A backup: what the dump added to the repository, compressed.</summary>
    public long? BytesAdded { get; set; }

    /// <summary>A retention: the size of the whole repository once pruned.</summary>
    public long? RepositoryBytes { get; set; }

    /// <summary>Why the step failed, or what a restore restored.</summary>
    public string? Detail { get; set; }

    /// <summary>When the row reached the database, set by the server.</summary>
    public DateTimeOffset RecordedAt { get; set; }
}
