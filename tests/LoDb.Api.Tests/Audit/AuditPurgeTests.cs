using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;

namespace LoDb.Api.Tests.Audit;

/// <summary>
/// <c>POST /api/admin/audit/purge</c>: an operator deletes the days before a date, what the
/// retention would delete, or everything, and the purge itself is recorded after it.
/// </summary>
public sealed class AuditPurgeTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string PurgePath = "/api/admin/audit/purge";
    private const string DayFormat = "yyyy-MM-dd";

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private DateTimeOffset Today => new(App.Clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);

    [Fact]
    public async Task PurgeBeforeADayDeletesTheEarlierDaysAndIsAudited()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await JournalRows.ClearAsync(App.Services);
        await JournalRows.AddAsync(
            App.Services,
            JournalRows.Entry(Today.AddDays(-10), AuditAction.BuildCreate),
            JournalRows.Entry(Today.AddDays(-5), AuditAction.BuildUpdate),
            JournalRows.Entry(Today.AddHours(1), AuditAction.BuildDelete));
        var before = Today.AddDays(-5);

        using var response = await admin.PostAsync(
            PurgePath,
            new
            {
                scope = "before",
                before = before.ToString(DayFormat, CultureInfo.InvariantCulture),
            });

        var receipt = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal(1, receipt.GetProperty("deleted").GetInt32());
        Assert.Equal(before, receipt.GetProperty("before").GetDateTimeOffset());
        var journal = await JournalRows.AllAsync(App.Services);
        Assert.Equal(
            [AuditAction.BuildUpdate, AuditAction.BuildDelete, AuditAction.AdminLogsPurge],
            journal.Select(static entry => entry.Action));
        var purge = journal[^1];
        Assert.Equal(
            (AuditActorType.Admin, AdminBrowser.AdminName),
            (purge.ActorType, purge.Actor));
        Assert.Equal("before", AccountsApp.Meta(purge, "scope"));
        Assert.Equal(
            before.ToString(DayFormat, CultureInfo.InvariantCulture),
            AccountsApp.Meta(purge, "before"));
        using var meta = JsonDocument.Parse(purge.Meta!);
        Assert.Equal(1, meta.RootElement.GetProperty("entries").GetInt32());
    }

    [Fact]
    public async Task PurgeOfEverythingLeavesItsOwnTrace()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await JournalRows.AddAsync(
            App.Services,
            JournalRows.Entry(Today.AddDays(-1), AuditAction.UserLogin));

        using var response = await admin.PostAsync(PurgePath, new { scope = "all" });

        var receipt = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.True(receipt.GetProperty("deleted").GetInt32() >= 1);
        var purge = Assert.Single(await JournalRows.AllAsync(App.Services));
        Assert.Equal(AuditAction.AdminLogsPurge, purge.Action);
        Assert.Equal("all", AccountsApp.Meta(purge, "scope"));
    }

    [Fact]
    public async Task RetentionPurgeFollowsTheClock()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await JournalRows.ClearAsync(App.Services);
        var cutoff = Today.AddMonths(-6);
        await JournalRows.AddAsync(
            App.Services,
            JournalRows.Entry(cutoff.AddSeconds(-1), AuditAction.BuildCreate),
            JournalRows.Entry(cutoff, AuditAction.BuildUpdate));

        using var response = await admin.PostAsync(PurgePath, new { scope = "retention" });

        var receipt = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal(1, receipt.GetProperty("deleted").GetInt32());
        Assert.Equal(cutoff, receipt.GetProperty("before").GetDateTimeOffset());
        Assert.Equal(
            [AuditAction.BuildUpdate, AuditAction.AdminLogsPurge],
            (await JournalRows.AllAsync(App.Services)).Select(static entry => entry.Action));
    }

    [Fact]
    public async Task InvalidPurgeDeletesNothing()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var recorded = (await JournalRows.AllAsync(App.Services)).Count;

        using var noScope = await admin.PostAsync(PurgePath, new { scope = "" });
        using var unknown = await admin.PostAsync(PurgePath, new { scope = "week" });
        using var noDay = await admin.PostAsync(PurgePath, new { scope = "before" });

        Assert.Equal(["required"], (await ApiJson.FieldErrorsAsync(noScope))["scope"]);
        Assert.Equal(["invalid-scope"], (await ApiJson.FieldErrorsAsync(unknown))["scope"]);
        Assert.Equal(["required"], (await ApiJson.FieldErrorsAsync(noDay))["before"]);
        Assert.Equal(recorded, (await JournalRows.AllAsync(App.Services)).Count);
    }

    [Fact]
    public async Task PurgeWithoutTheXsrfTokenIsRefused()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        using var request = admin.Post(PurgePath, new { scope = "all" });
        request.Headers.Remove(BrowserClient.XsrfHeader);

        using var response = await admin.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(
            await JournalRows.AllAsync(App.Services),
            static entry => entry.Action == AuditAction.AdminLogsPurge);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
