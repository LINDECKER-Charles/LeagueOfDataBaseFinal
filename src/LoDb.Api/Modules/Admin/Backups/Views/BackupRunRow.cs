namespace LoDb.Api.Modules.Admin.Backups.Views;

/// <summary>A step of a pass of the backups, as the container reported it.</summary>
internal sealed record BackupRunRow
{
    public required long Id { get; init; }

    /// <summary>The database of a backup or a restore test; null for the repository.</summary>
    public string? Database { get; init; }

    /// <summary>backup, retention, restore or check.</summary>
    public required string Step { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }

    public required bool Succeeded { get; init; }

    /// <summary>A backup: the size of the dump.</summary>
    public long? BytesRead { get; init; }

    /// <summary>A backup: what it added to the repository, compressed.</summary>
    public long? BytesAdded { get; init; }

    /// <summary>A retention: the size of the repository once pruned.</summary>
    public long? RepositoryBytes { get; init; }

    /// <summary>Why the step failed, or what a restore test restored.</summary>
    public string? Detail { get; init; }
}
