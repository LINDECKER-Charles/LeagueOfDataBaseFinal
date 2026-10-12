namespace LoDb.Api.Modules.Admin.Backups.Views;

/// <summary>
/// The off-host backups of the host as their journal tells them: the state of each database,
/// the repository and what it is estimated to cost, then the steps of a window of days.
/// </summary>
internal sealed record BackupReport
{
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>The days the steps cover, back from the time of the report.</summary>
    public required int Days { get; init; }

    /// <summary>Every database the journal names, by name; empty before the first pass.</summary>
    public required IReadOnlyList<BackupDatabaseState> Databases { get; init; }

    /// <summary>The repository as its last retention measured it; null before any.</summary>
    public BackupRepositoryState? Repository { get; init; }

    /// <summary>The steps of the window, newest first, the latest 1000 at most.</summary>
    public required IReadOnlyList<BackupRunRow> Runs { get; init; }
}
