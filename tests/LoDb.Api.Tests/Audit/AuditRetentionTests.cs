using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Api.Workers;
using LoDb.Api.Workers.Audit;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Jobs;
using LoDb.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Audit;

/// <summary>
/// The daily retention job, on a simulated clock: whatever is older than six months goes,
/// day after day, without anyone asking.
/// </summary>
public sealed class AuditRetentionTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void RetentionJobIsDiscoveredAsAWorker()
    {
        Assert.Contains(
            typeof(AuditRetentionJob),
            ConventionalWorkers.Discover(typeof(Program).Assembly));
    }

    [Fact]
    public async Task EntriesOlderThanSixMonthsAreDeletedDayAfterDay()
    {
        var today = new DateTimeOffset(App.Clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        var cutoff = today.AddMonths(-6);
        await JournalRows.AddAsync(
            App.Services,
            JournalRows.Entry(cutoff.AddDays(-30), AuditAction.BuildCreate),
            JournalRows.Entry(cutoff.AddHours(-1), AuditAction.BuildUpdate),
            JournalRows.Entry(cutoff.AddHours(1), AuditAction.BuildDelete),
            JournalRows.Entry(today, AuditAction.BuildVote));
        var job = ActivatorUtilities.CreateInstance<AuditRetentionJob>(App.Services);

        var first = await job.RunIfDueAsync(Cancellation);
        var kept = await ActionsAsync();
        var early = await job.RunIfDueAsync(Cancellation);
        App.Clock.Advance(TimeSpan.FromDays(1));
        var next = await job.RunIfDueAsync(Cancellation);

        Assert.Equal(JobRunOutcome.Succeeded, first);
        Assert.Equal([AuditAction.BuildDelete, AuditAction.BuildVote], kept);
        Assert.Equal(JobRunOutcome.NotDue, early);
        Assert.Equal(JobRunOutcome.Succeeded, next);
        Assert.Equal([AuditAction.BuildVote], await ActionsAsync());
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private async Task<List<AuditAction>> ActionsAsync() =>
        [.. (await JournalRows.AllAsync(App.Services)).Select(static entry => entry.Action)];
}
