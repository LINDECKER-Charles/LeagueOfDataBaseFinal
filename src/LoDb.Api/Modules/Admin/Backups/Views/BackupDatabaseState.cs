namespace LoDb.Api.Modules.Admin.Backups.Views;

/// <summary>
/// The backups of one database, whatever the window: their state, and its restore tests'.
/// </summary>
internal sealed record BackupDatabaseState
{
    /// <summary>The database as infra-vps names it: lodb, umami.</summary>
    public required string Name { get; init; }

    /// <summary>ok, late (no success for 26 hours) or failed (the last attempt).</summary>
    public required string Status { get; init; }

    /// <summary>End of the last backup that succeeded.</summary>
    public DateTimeOffset? LastSuccessAt { get; init; }

    /// <summary>End of the last backup that failed.</summary>
    public DateTimeOffset? LastFailureAt { get; init; }

    /// <summary>Size of the dump of the last backup that succeeded.</summary>
    public long? LastBytesRead { get; init; }

    /// <summary>
    /// ok, late (no success for 35 days) or failed; null while no restore was ever tested.
    /// </summary>
    public string? RestoreStatus { get; init; }

    /// <summary>End of the last restore test that succeeded.</summary>
    public DateTimeOffset? LastRestoreAt { get; init; }
}
