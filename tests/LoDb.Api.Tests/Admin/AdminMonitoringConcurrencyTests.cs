using System.Text.Json;
using LoDb.Api.Modules.Admin.Monitoring;
using LoDb.Api.Modules.Admin.Monitoring.Views;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Persistence;
using LoDb.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// The monitoring report shared by requests that overlap: the report of the cache never
/// reads through the request that started it, and a report whose database reads failed is
/// not kept.
/// </summary>
public sealed class AdminMonitoringConcurrencyTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres), IClassFixture<PostgresContainerFixture>
{
    private const string MonitoringPath = "/api/admin/monitoring";
    private const string RefreshPath = MonitoringPath + "?refresh=true";
    private const string HideOutbox = "ALTER TABLE email_outbox RENAME TO email_outbox_away";
    private const string RestoreOutbox = "ALTER TABLE email_outbox_away RENAME TO email_outbox";
    private const int Readers = 8;

    [Fact]
    public async Task AReportOutlivesTheRequestThatStartedIt()
    {
        await App.SeedAsync(new AccountSeed());
        using var aborted = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var starter = App.Services.CreateAsyncScope();
        var started = ReporterOf(starter).ReportAsync(refresh: true, aborted.Token);
        await using var joiner = App.Services.CreateAsyncScope();
        var joined = ReporterOf(joiner).ReportAsync(refresh: false, Cancellation);

        // The request that started the report is aborted and its scope ends mid-read.
        await aborted.CancelAsync();
        await starter.DisposeAsync();
        await IgnoreCancellationAsync(started);

        var report = await joined;
        Assert.Equal(1, report.Counters?.UsersTotal);
        Assert.NotNull(report.Versions);
        Assert.Equal(0, report.Ingestion.OutboxPending);
        Assert.NotEmpty(report.Tables);
    }

    [Fact]
    public async Task OverlappingReadsAndRefreshesAllGetTheFigures()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        var reads = Enumerable.Range(0, Readers)
            .Select(index => ReadAsync(admin, index % 2 == 0 ? RefreshPath : MonitoringPath));
        var reports = await Task.WhenAll(reads);
        var cached = await ReadAsync(admin, MonitoringPath);

        Assert.All(reports.Append(cached), static report =>
        {
            Assert.Equal(1, report.GetProperty("counters").GetProperty("usersTotal").GetInt32());
            Assert.Equal(JsonValueKind.Object, report.GetProperty("versions").ValueKind);
            Assert.NotEqual(0, report.GetProperty("tables").GetArrayLength());
        });
    }

    [Fact]
    public async Task AReportWithUnreadableFiguresIsNotKept()
    {
        await ExecuteAsync(HideOutbox);
        var degraded = await ReportAsync();
        await ExecuteAsync(RestoreOutbox);
        await App.SeedAsync(new AccountSeed());

        var next = await ReportAsync();

        Assert.Null(degraded.Ingestion.OutboxPending);
        Assert.Equal(0, degraded.Counters?.UsersTotal);
        Assert.Equal(0, next.Ingestion.OutboxPending);
        Assert.Equal(1, next.Counters?.UsersTotal);
    }

    private static MonitoringReporter ReporterOf(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<MonitoringReporter>();

    // Not refreshed: only a report that was not kept can show the account seeded since.
    private async Task<MonitoringReport> ReportAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await ReporterOf(scope).ReportAsync(refresh: false, Cancellation);
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // The aborted request gives up its wait; the report goes on for the others.
        }
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoDbDbContext>();
        await db.Database.ExecuteSqlRawAsync(sql, Cancellation);
    }
}
