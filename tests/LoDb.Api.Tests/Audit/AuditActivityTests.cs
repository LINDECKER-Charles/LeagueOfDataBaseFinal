using System.Globalization;
using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Audit;
using LoDb.Testing;

namespace LoDb.Api.Tests.Audit;

/// <summary>
/// <c>GET /api/admin/audit/users/{userId}</c>: what one account did or underwent, by id as
/// actor or target, and by its e-mail or username as the legacy journal also matched it.
/// </summary>
public sealed class AuditActivityTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Day = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task ActivityGathersTheActionsOfAndOnTheAccount()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var member = await App.SeedAsync(new AccountSeed());
        await JournalRows.ClearAsync(App.Services);
        var id = member.Id.ToString(CultureInfo.InvariantCulture);
        var acted = Acted(Day.AddHours(1), member.Id);
        var banned = JournalRows.Entry(Day.AddHours(2), AuditAction.AdminUserBan);
        banned.TargetType = AuditTargetType.User;
        banned.TargetId = id;
        var typed = Anonymous(Day.AddHours(3), AccountSeed.Address.ToUpperInvariant());
        var unrelated = JournalRows.Entry(Day.AddHours(4), AuditAction.UserLogin);
        await JournalRows.AddAsync(App.Services, acted, banned, typed, unrelated);

        var answer = await ActivityAsync(admin, member.Id, string.Empty);

        Assert.Equal(AccountSeed.Name, ApiJson.Text(answer.GetProperty("subject"), "username"));
        Assert.Equal([Day.AddHours(3), Day.AddHours(2), Day.AddHours(1)], Times(answer));
    }

    [Fact]
    public async Task DeletedAccountKeepsItsTrailById()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await JournalRows.ClearAsync(App.Services);
        await JournalRows.AddAsync(
            App.Services,
            Acted(Day, 4242),
            Anonymous(Day.AddHours(1), "gone@example.test"));

        var answer = await ActivityAsync(admin, 4242, string.Empty);

        Assert.Equal(JsonValueKind.Null, answer.GetProperty("subject").ValueKind);
        Assert.Equal([Day], Times(answer));
    }

    [Fact]
    public async Task ActivityTakesTheFiltersOfTheJournal()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        await JournalRows.ClearAsync(App.Services);
        var logout = Acted(Day.AddHours(1), 99);
        logout.Action = AuditAction.UserLogout;
        await JournalRows.AddAsync(App.Services, Acted(Day, 99), logout);

        var answer = await ActivityAsync(admin, 99, "?action=user.logout");

        Assert.Equal([Day.AddHours(1)], Times(answer));
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static AuditLogEntry Acted(DateTimeOffset at, int id)
    {
        var entry = JournalRows.Entry(at, AuditAction.BuildCreate);
        entry.ActorId = id;
        return entry;
    }

    // A failed sign-in names the account by what was typed, whatever its case.
    private static AuditLogEntry Anonymous(
        DateTimeOffset at,
        string typed)
    {
        var entry = JournalRows.Entry(at, AuditAction.UserLoginFailed);
        entry.ActorType = AuditActorType.Anonymous;
        entry.ActorId = null;
        entry.Actor = null;
        entry.Target = typed;
        return entry;
    }

    private static async Task<JsonElement> ActivityAsync(
        BrowserClient admin,
        int userId,
        string query)
    {
        using var response = await admin.GetAsync($"/api/admin/audit/users/{userId}{query}");
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    private static List<DateTimeOffset> Times(JsonElement answer) =>
        [.. answer.GetProperty("activity").GetProperty("items").EnumerateArray()
            .Select(static item => item.GetProperty("occurredAt").GetDateTimeOffset())];
}
