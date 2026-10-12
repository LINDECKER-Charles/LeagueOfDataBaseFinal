using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin.OwnHost;

/// <summary>
/// The lists of the panels: what a search or a filter keeps, and the counters they show over
/// the whole table of the users, the keys of the public API, the builds and the contact
/// messages.
/// </summary>
/// <remarks>A host per test: each counts the rows of a whole table.</remarks>
public sealed class AdminListsTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres)
{
    private const string UsersPath = "/api/admin/users";
    private const string ClientsPath = "/api/admin/api-clients";
    private const string BuildsPath = "/api/admin/builds";
    private const string ContactsPath = "/api/admin/contacts";

    [Fact]
    public async Task SearchMatchesUsernamesAndEmails()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var member = await App.SeedAsync(new AccountSeed { RiotTagline = "EUW" });
        await AdminSeed.BuildAsync(App, member.Id, "Ahri mid");

        var byName = await ReadAsync(admin, UsersPath + "?q=legende_");
        var byEmail = await ReadAsync(admin, UsersPath + "?q=OPERATRICE@example");
        var all = await ReadAsync(admin, UsersPath);

        var found = Assert.Single(byName.GetProperty("items").EnumerateArray());
        Assert.Equal(
            (member.Id, AccountSeed.Name, 1, false),
            (found.GetProperty("id").GetInt32(), ApiJson.Text(found, "username"),
                found.GetProperty("buildCount").GetInt32(),
                found.GetProperty("isAdmin").GetBoolean()));
        Assert.Equal("EUW", ApiJson.Text(found, "riotTagline"));
        var operatrice = Assert.Single(byEmail.GetProperty("items").EnumerateArray());
        Assert.True(operatrice.GetProperty("isAdmin").GetBoolean());
        Assert.Equal(JsonValueKind.Null, operatrice.GetProperty("riotTagline").ValueKind);
        Assert.True(operatrice.GetProperty("twoFactorEnabled").GetBoolean());
        Assert.Equal(2, all.GetProperty("total").GetInt32());
        Assert.Equal(2, all.GetProperty("stats").GetProperty("total").GetInt32());
        Assert.Equal(1, all.GetProperty("pages").GetInt32());
    }

    [Fact]
    public async Task TheListShowsEachKeyWithItsOwnerAndTheFleetCounters()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var owner = await App.SeedAsync(new AccountSeed());
        var (id, secret) = await AdminSeed.ApiKeyAsync(App, owner.Id);

        var page = await ReadAsync(admin, ClientsPath);

        var key = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(
            (id, secret[..12], "free", true, AccountSeed.Name),
            (key.GetProperty("id").GetInt32(), ApiJson.Text(key, "keyPrefix"),
                ApiJson.Text(key, "plan"), key.GetProperty("isActive").GetBoolean(),
                ApiJson.Text(key.GetProperty("owner"), "username")));
        var kpis = page.GetProperty("kpis");
        Assert.Equal(1, kpis.GetProperty("active").GetInt32());
        var plan = Assert.Single(kpis.GetProperty("byPlan").EnumerateArray());
        Assert.Equal(
            ("free", 1),
            (ApiJson.Text(plan, "plan"), plan.GetProperty("keys").GetInt32()));
    }

    [Fact]
    public async Task BuildsAreSearchedByNameAndVisibility()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var owner = await App.SeedAsync(new AccountSeed());
        var ahri = await AdminSeed.BuildAsync(App, owner.Id, "Ahri mid");
        await AdminSeed.BuildAsync(App, owner.Id, "Garen top");
        using var hide = await admin.PostAsync($"{BuildsPath}/{ahri}/unpublish");

        var byName = await ReadAsync(admin, BuildsPath + "?q=AHRI");
        var hidden = await ReadAsync(admin, BuildsPath + "?visibility=private");
        var shown = await ReadAsync(admin, BuildsPath + "?visibility=public");

        var found = Assert.Single(byName.GetProperty("items").EnumerateArray());
        Assert.Equal(
            (ahri, false, AccountSeed.Name),
            (found.GetProperty("id").GetInt32(), found.GetProperty("isPublic").GetBoolean(),
                ApiJson.Text(found.GetProperty("owner"), "username")));
        Assert.Equal(ahri, Assert.Single(hidden.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetInt32());
        Assert.Equal("Garen top", ApiJson.Text(
            Assert.Single(shown.GetProperty("items").EnumerateArray()), "name"));
        var stats = shown.GetProperty("stats");
        Assert.Equal(
            (2, 1),
            (stats.GetProperty("total").GetInt32(), stats.GetProperty("public").GetInt32()));
    }

    [Fact]
    public async Task ContactMessagesAreHandledReopenedAndDeleted()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var message = await AdminSeed.ContactAsync(App, "Lien cassé");

        var fresh = await ReadAsync(admin, ContactsPath + "?status=new");
        using var handle = await admin.PostAsync($"{ContactsPath}/{message}/handle");
        var handled = await ReadAsync(admin, ContactsPath + "?status=handled");
        using var reopen = await admin.PostAsync($"{ContactsPath}/{message}/reopen");
        var reopened = await ReadAsync(admin, ContactsPath);
        using var deletion = await AdminCalls.DeleteAsync(admin, $"{ContactsPath}/{message}");

        Assert.Equal("Lien cassé", ApiJson.Text(
            Assert.Single(fresh.GetProperty("items").EnumerateArray()), "subject"));
        Assert.Equal(
            (HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.NoContent),
            (handle.StatusCode, reopen.StatusCode, deletion.StatusCode));
        var done = Assert.Single(handled.GetProperty("items").EnumerateArray());
        Assert.Equal("handled", ApiJson.Text(done, "status"));
        Assert.NotEqual(JsonValueKind.Null, done.GetProperty("handledAt").ValueKind);
        var open = Assert.Single(reopened.GetProperty("items").EnumerateArray());
        Assert.Equal("new", ApiJson.Text(open, "status"));
        Assert.Equal(JsonValueKind.Null, open.GetProperty("handledAt").ValueKind);
        Assert.Equal(1, reopened.GetProperty("stats").GetProperty("new").GetInt32());
        Assert.Equal(0, (await ReadAsync(admin, ContactsPath)).GetProperty("total").GetInt32());
    }
}
