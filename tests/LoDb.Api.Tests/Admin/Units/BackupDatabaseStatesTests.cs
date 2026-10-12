using LoDb.Api.Modules.Admin.Backups;
using LoDb.Infrastructure.Persistence.Backups;

namespace LoDb.Api.Tests.Admin.Units;

/// <summary>
/// The state of each database from the newest steps of the journal: ok, late or failed
/// against the deadlines of infra-vps's alerts.
/// </summary>
public sealed class BackupDatabaseStatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecentSuccessIsOkWithTheSizeOfItsDump()
    {
        var backup = Backup("lodb", TimeSpan.FromHours(7), isSuccess: true);
        backup.BytesRead = 4096;

        var state = Assert.Single(BackupDatabaseStates.Of([backup], Now));

        Assert.Equal(
            ("lodb", BackupStatuses.Ok, (long?)4096, (string?)null),
            (state.Name, state.Status, state.LastBytesRead, state.RestoreStatus));
    }

    [Fact]
    public void SuccessOlderThanADayAndTwoHoursIsLate()
    {
        var state = Assert.Single(BackupDatabaseStates.Of(
            [Backup("lodb", TimeSpan.FromHours(27), isSuccess: true)],
            Now));

        Assert.Equal(BackupStatuses.Late, state.Status);
    }

    [Fact]
    public void FailureAfterTheLastSuccessFailsEvenWithinTheDeadline()
    {
        var states = BackupDatabaseStates.Of(
            [
                Backup("umami", TimeSpan.FromHours(30), isSuccess: true),
                Backup("umami", TimeSpan.FromHours(3), isSuccess: false),
                Backup("lodb", TimeSpan.FromHours(3), isSuccess: false),
                Backup("lodb", TimeSpan.FromHours(2), isSuccess: true),
            ],
            Now);

        Assert.Equal(
            [("lodb", BackupStatuses.Ok), ("umami", BackupStatuses.Failed)],
            states.Select(static state => (state.Name, state.Status)));
    }

    [Fact]
    public void DatabaseThatNeverSucceededFailsOrIsLate()
    {
        var states = BackupDatabaseStates.Of(
            [
                Backup("lodb", TimeSpan.FromHours(1), isSuccess: false),
                Restore("umami", TimeSpan.FromDays(1), isSuccess: true),
            ],
            Now);

        Assert.Equal(
            [("lodb", BackupStatuses.Failed), ("umami", BackupStatuses.Late)],
            states.Select(static state => (state.Name, state.Status)));
    }

    [Fact]
    public void RestoreTestsFollowTheirOwnMonthlyDeadline()
    {
        var states = BackupDatabaseStates.Of(
            [
                Backup("lodb", TimeSpan.FromHours(1), isSuccess: true),
                Restore("lodb", TimeSpan.FromDays(34), isSuccess: true),
                Backup("umami", TimeSpan.FromHours(1), isSuccess: true),
                Restore("umami", TimeSpan.FromDays(36), isSuccess: true),
                Backup("vault", TimeSpan.FromHours(1), isSuccess: true),
                Restore("vault", TimeSpan.FromDays(40), isSuccess: true),
                Restore("vault", TimeSpan.FromDays(5), isSuccess: false),
            ],
            Now);

        Assert.Equal(
            [BackupStatuses.Ok, BackupStatuses.Late, BackupStatuses.Failed],
            states.Select(static state => state.RestoreStatus));
        Assert.Equal(Now.AddDays(-40), states[2].LastRestoreAt);
    }

    private static BackupRun Backup(string database, TimeSpan age, bool isSuccess) => new()
    {
        Database = database,
        Step = BackupStep.Backup,
        StartedAt = Now - age,
        FinishedAt = Now - age,
        Succeeded = isSuccess,
    };

    private static BackupRun Restore(string database, TimeSpan age, bool isSuccess) => new()
    {
        Database = database,
        Step = BackupStep.Restore,
        StartedAt = Now - age,
        FinishedAt = Now - age,
        Succeeded = isSuccess,
    };
}
