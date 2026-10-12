using LoDb.Api.Modules.Admin.Backups.Views;
using LoDb.Infrastructure.Persistence.Backups;

namespace LoDb.Api.Modules.Admin.Backups;

/// <summary>
/// The state of each database, from the latest step of each kind and outcome the journal
/// holds for it: a pure function of those rows and the time of the report.
/// </summary>
internal static class BackupDatabaseStates
{
    /// <summary>
    /// One state per database the <paramref name="latest"/> rows name, by name. The rows are
    /// the newest of each database, step and outcome; others are ignored.
    /// </summary>
    public static IReadOnlyList<BackupDatabaseState> Of(
        IEnumerable<BackupRun> latest,
        DateTimeOffset now) =>
        [
            .. latest
                .Where(static run => run.Database is not null)
                .GroupBy(static run => run.Database!, StringComparer.Ordinal)
                .OrderBy(static database => database.Key, StringComparer.Ordinal)
                .Select(database => StateOf(database.Key, [.. database], now)),
        ];

    private static BackupDatabaseState StateOf(
        string name,
        IReadOnlyList<BackupRun> runs,
        DateTimeOffset now)
    {
        var backups = runs.Where(static run => run.Step == BackupStep.Backup).ToList();
        var success = Newest(backups.Where(static run => run.Succeeded));
        var failure = Newest(backups.Where(static run => !run.Succeeded));
        var restores = runs.Where(static run => run.Step == BackupStep.Restore).ToList();
        var restored = Newest(restores.Where(static run => run.Succeeded));
        return new BackupDatabaseState
        {
            Name = name,
            Status = BackupStatuses.Of(
                success?.FinishedAt,
                failure?.FinishedAt,
                now - BackupStatuses.BackupDeadline),
            LastSuccessAt = success?.FinishedAt,
            LastFailureAt = failure?.FinishedAt,
            LastBytesRead = success?.BytesRead,
            RestoreStatus = RestoreStatusOf(restores, now),
            LastRestoreAt = restored?.FinishedAt,
        };
    }

    // Null while no restore was ever tested: the first test runs with the first pass.
    private static string? RestoreStatusOf(
        List<BackupRun> restores,
        DateTimeOffset now) =>
        restores.Count == 0
            ? null
            : BackupStatuses.Of(
                Newest(restores.Where(static run => run.Succeeded))?.FinishedAt,
                Newest(restores.Where(static run => !run.Succeeded))?.FinishedAt,
                now - BackupStatuses.RestoreDeadline);

    private static BackupRun? Newest(IEnumerable<BackupRun> runs) =>
        runs.MaxBy(static run => run.FinishedAt);
}
