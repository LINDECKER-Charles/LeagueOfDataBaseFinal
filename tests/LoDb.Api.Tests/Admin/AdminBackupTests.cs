using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Persistence.Backups;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// The backups panel: the journal the backup container writes, read back as the state of
/// each database, the repository with its estimated cost, and the steps of a window.
/// </summary>
public sealed class AdminBackupTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres)
{
    private const long RepositoryBytes = 3_000_000_000;

    [Fact]
    public async Task JournalShowsEachDatabaseTheRepositoryAndItsCost()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedAsync(
            Done("lodb", BackupStep.Backup, TimeSpan.FromHours(2)),
            Failed("umami", TimeSpan.FromHours(1)),
            Done(null, BackupStep.Retention, TimeSpan.FromMinutes(50)),
            Done("lodb", BackupStep.Backup, TimeSpan.FromDays(40)));

        var report = await ReadAsync(admin, "/api/admin/backups");

        Assert.Equal(30, report.GetProperty("days").GetInt32());
        Assert.Equal(
            [("lodb", "ok"), ("umami", "failed")],
            report.GetProperty("databases").EnumerateArray().Select(static state =>
                (ApiJson.Text(state, "name"), ApiJson.Text(state, "status"))));
        var repository = report.GetProperty("repository");
        Assert.Equal(RepositoryBytes, repository.GetProperty("bytes").GetInt64());
        Assert.Equal(0.0482m, repository.GetProperty("monthlyCost").GetDecimal());
        Assert.Equal("EUR", ApiJson.Text(repository, "currency"));
        Assert.Equal(
            ["retention", "backup", "backup"],
            report.GetProperty("runs").EnumerateArray()
                .Select(static run => ApiJson.Text(run, "step")));
    }

    [Fact]
    public async Task WiderWindowReachesOlderSteps()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await SeedAsync(
            Done("lodb", BackupStep.Backup, TimeSpan.FromHours(2)),
            Done("lodb", BackupStep.Restore, TimeSpan.FromDays(40)));

        var month = await ReadAsync(admin, "/api/admin/backups");
        var quarter = await ReadAsync(admin, "/api/admin/backups?days=90");

        Assert.Equal(1, month.GetProperty("runs").GetArrayLength());
        Assert.Equal(2, quarter.GetProperty("runs").GetArrayLength());
        var lodb = Assert.Single(quarter.GetProperty("databases").EnumerateArray());
        Assert.Equal("late", ApiJson.Text(lodb, "restoreStatus"));
    }

    [Fact]
    public async Task EmptyJournalReportsNothingYet()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        var report = await ReadAsync(admin, "/api/admin/backups?days=0");

        Assert.Equal(1, report.GetProperty("days").GetInt32());
        Assert.Equal(0, report.GetProperty("databases").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("repository").ValueKind);
        Assert.Equal(0, report.GetProperty("runs").GetArrayLength());
    }

    private BackupRun Done(string? database, BackupStep step, TimeSpan age)
    {
        var finishedAt = App.Clock.GetUtcNow() - age;
        return new BackupRun
        {
            Database = database,
            Step = step,
            StartedAt = finishedAt.AddMinutes(-1),
            FinishedAt = finishedAt,
            Succeeded = true,
            RepositoryBytes = step == BackupStep.Retention ? RepositoryBytes : null,
        };
    }

    private BackupRun Failed(string database, TimeSpan age)
    {
        var run = Done(database, BackupStep.Backup, age);
        run.Succeeded = false;
        run.Detail = "connexion refusee";
        return run;
    }

    private Task<int> SeedAsync(params BackupRun[] runs) =>
        AdminSeed.WithContextAsync(App, async context =>
        {
            context.BackupRuns.AddRange(runs);
            return await context.SaveChangesAsync(Cancellation);
        });
}
