using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;

namespace LoDb.Api.Tests.Audit;

/// <summary>
/// <c>GET /api/admin/audit</c>: the journal is the administrators' alone, newest first, paged
/// by one row read past the page, and filtered by action, group, outcome, actor and period.
/// </summary>
public sealed class AuditJournalTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string JournalPath = "/api/admin/audit";

    private static readonly DateTimeOffset Day = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task JournalIsForAdministratorsOnly()
    {
        using var visitor = App.Browser();
        using var member = await AdminBrowser.OpenMemberAsync(App);
        using var admin = await AdminBrowser.OpenAsync(App);

        using var anonymous = await visitor.GetAsync(JournalPath);
        using var refused = await member.GetAsync(JournalPath);
        using var allowed = await admin.GetAsync(JournalPath);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal("no-store", allowed.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task JournalIsNewestFirstAndPaged()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await JournalRows.ClearAsync(App.Services);
        await JournalRows.AddAsync(
            App.Services,
            [.. Enumerable.Range(0, 5).Select(hour =>
                JournalRows.Entry(Day.AddHours(hour), AuditAction.BuildCreate))]);

        var first = await PageAsync(admin, "?pageSize=2");
        var last = await PageAsync(admin, "?pageSize=2&page=3");

        Assert.Equal([Day.AddHours(4), Day.AddHours(3)], Times(first));
        Assert.True(first.GetProperty("hasMore").GetBoolean());
        Assert.Equal([Day], Times(last));
        Assert.False(last.GetProperty("hasMore").GetBoolean());
        Assert.Equal(3, last.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task EntryCarriesItsCodesAddressAndDetails()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await JournalRows.ClearAsync(App.Services);
        var entry = JournalRows.Entry(Day, AuditAction.AdminApiClientCredit);
        entry.TargetType = AuditTargetType.ApiClient;
        entry.TargetId = "client-1";
        entry.Meta = """{"requests":500}""";
        await JournalRows.AddAsync(App.Services, entry);

        var item = (await PageAsync(admin, string.Empty)).GetProperty("items")[0];

        Assert.Equal("admin.api_client_credit", ApiJson.Text(item, "action"));
        Assert.Equal("admin", ApiJson.Text(item, "category"));
        Assert.Equal("success", ApiJson.Text(item, "outcome"));
        Assert.Equal("user", ApiJson.Text(item, "actorType"));
        Assert.Equal("api_client", ApiJson.Text(item, "targetType"));
        Assert.Equal("203.0.113.9", ApiJson.Text(item, "ip"));
        Assert.Equal(500, item.GetProperty("meta").GetProperty("requests").GetInt32());
    }

    [Fact]
    public async Task FiltersNarrowTheJournal()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await JournalRows.ClearAsync(App.Services);
        var failed = JournalRows.Entry(Day.AddDays(-3), AuditAction.UserLoginFailed);
        failed.Outcome = AuditOutcome.Failure;
        var other = JournalRows.Entry(Day.AddDays(-1), AuditAction.BuildVote);
        other.ActorId = 8;
        other.Actor = "Autre_8";
        await JournalRows.AddAsync(
            App.Services,
            failed,
            other,
            JournalRows.Entry(Day, AuditAction.UserLogin));

        Assert.Equal([Day.AddDays(-3)], Times(await PageAsync(admin, "?outcome=failure")));
        Assert.Equal([Day, Day.AddDays(-3)], Times(await PageAsync(admin, "?category=auth")));
        Assert.Equal(
            [Day.AddDays(-1)],
            Times(await PageAsync(admin, "?action=build.vote&action=user.logout")));
        Assert.Equal([Day.AddDays(-1)], Times(await PageAsync(admin, "?actor=autre_8")));
        Assert.Empty(Times(await PageAsync(admin, "?actor=autre%258")));
        Assert.Equal([Day, Day.AddDays(-3)], Times(await PageAsync(admin, "?actorId=7")));
        Assert.Equal(
            [Day.AddDays(-1), Day.AddDays(-3)],
            Times(await PageAsync(admin, "?from=2026-09-17&to=2026-09-19")));
        Assert.Empty(Times(await PageAsync(admin, "?category=build&action=user.login")));
    }

    [Fact]
    public async Task InvalidFiltersAreReportedTogether()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.GetAsync(
            JournalPath + "?action=user.dance&category=misc&outcome=maybe&actorType=robot"
            + "&from=2026-09-20&to=2026-09-01&page=0&pageSize=101");

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["unknown-action"], errors["action"]);
        Assert.Equal(["unknown-category"], errors["category"]);
        Assert.Equal(["unknown-outcome"], errors["outcome"]);
        Assert.Equal(["unknown-actor-type"], errors["actorType"]);
        Assert.Equal(["invalid-range"], errors["to"]);
        Assert.Equal(["out-of-range"], errors["page"]);
        Assert.Equal(["out-of-range"], errors["pageSize"]);
    }

    [Fact]
    public async Task VocabularyListsEveryActionWithItsGroup()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        using var response = await admin.GetAsync(JournalPath + "/vocabulary");

        var vocabulary = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        var actions = vocabulary.GetProperty("actions").EnumerateArray().ToList();
        Assert.Equal(Enum.GetValues<AuditAction>().Length, actions.Count);
        Assert.Contains(
            actions,
            static action => ApiJson.Text(action, "name") == "admin.logs_purge"
                && ApiJson.Text(action, "category") == "admin");
        Assert.Equal(
            ["user", "build", "apikey", "api_client"],
            vocabulary.GetProperty("targetTypes").EnumerateArray()
                .Select(static type => type.GetString()));
    }

    [Fact]
    public async Task VolumeTellsTheSpanAndTheRetentionCutoff()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await JournalRows.ClearAsync(App.Services);
        await JournalRows.AddAsync(
            App.Services,
            JournalRows.Entry(Day.AddDays(-2), AuditAction.UserLogin),
            JournalRows.Entry(Day, AuditAction.UserLogout));

        using var response = await admin.GetAsync(JournalPath + "/volume");

        var volume = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        var cutoff = App.Clock.GetUtcNow().UtcDateTime.Date.AddMonths(-6);
        Assert.Equal(2, volume.GetProperty("entries").GetInt64());
        Assert.Equal(Day.AddDays(-2), volume.GetProperty("oldest").GetDateTimeOffset());
        Assert.Equal(Day, volume.GetProperty("newest").GetDateTimeOffset());
        Assert.Equal(6, volume.GetProperty("retentionMonths").GetInt32());
        Assert.Equal(cutoff, volume.GetProperty("retentionCutoff").GetDateTimeOffset().UtcDateTime);
        Assert.True(volume.GetProperty("totalBytes").GetInt64() > 0);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static async Task<JsonElement> PageAsync(BrowserClient admin, string query)
    {
        using var response = await admin.GetAsync(JournalPath + query);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    private static List<DateTimeOffset> Times(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray()
            .Select(static item => item.GetProperty("occurredAt").GetDateTimeOffset())];
}
