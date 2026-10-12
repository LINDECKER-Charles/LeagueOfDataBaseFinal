namespace LoDb.Infrastructure.Persistence.Backups;

/// <summary>
/// A step of a nightly pass of the host's off-host backups, stored as <c>backup</c>,
/// <c>retention</c>, <c>restore</c> or <c>check</c>.
/// </summary>
public enum BackupStep
{
    /// <summary>One database dumped into the restic repository.</summary>
    Backup,

    /// <summary>The retention of the repository, after which its size is measured.</summary>
    Retention,

    /// <summary>The monthly restore of a database's latest snapshot, in a spare server.</summary>
    Restore,

    /// <summary>The monthly read-back of a share of the repository's data.</summary>
    Check,
}
