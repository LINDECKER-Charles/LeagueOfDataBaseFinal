using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>/api/admin/users</c>: the search of the moderation list, the ban with its reason, the
/// unban and the deletion, each recorded in the audit journal.
/// </summary>
public sealed class AdminUserTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres), IClassFixture<PostgresContainerFixture>
{
    private const string UsersPath = "/api/admin/users";

    private static readonly Uri Usage = new("/v1/usage", UriKind.Relative);

    [Fact]
    public async Task SearchMatchesUsernamesAndEmails()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var member = await App.SeedAsync(new AccountSeed());
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
        var operatrice = Assert.Single(byEmail.GetProperty("items").EnumerateArray());
        Assert.True(operatrice.GetProperty("isAdmin").GetBoolean());
        Assert.True(operatrice.GetProperty("twoFactorEnabled").GetBoolean());
        Assert.Equal(2, all.GetProperty("total").GetInt32());
        Assert.Equal(2, all.GetProperty("stats").GetProperty("total").GetInt32());
        Assert.Equal(1, all.GetProperty("pages").GetInt32());
    }

    [Fact]
    public async Task ABanClosesTheSessionAndIsAuditedWithItsReason()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var member = await App.SeedAsync(new AccountSeed());
        using var memberBrowser = App.Browser();
        using var signIn = await memberBrowser.SignInAsync(AccountSeed.Name);

        using var ban = await admin.PostAsync(
            $"{UsersPath}/{member.Id}/ban",
            new { reason = "  Spam de liens  " });

        Assert.Equal(HttpStatusCode.NoContent, ban.StatusCode);
        var stored = await App.FindAsync(AccountSeed.Name);
        Assert.Equal((true, "Spam de liens"), (stored.IsBanned, stored.BanReason));
        Assert.NotNull(stored.BannedAt);
        var entry = await LastAuditAsync(AuditAction.AdminUserBan);
        Assert.Equal(
            (AuditActorType.Admin, AdminBrowser.AdminName, AuditTargetType.User),
            (entry.ActorType, entry.Actor, entry.TargetType));
        Assert.Equal(member.Id.ToString(CultureInfo.InvariantCulture), entry.TargetId);
        Assert.Equal("Spam de liens", AccountsApp.Meta(entry, "reason"));
        Assert.Null(await memberBrowser.SignedInAsAsync());
        using var again = App.Browser();
        using var refused = await again.SignInAsync(AccountSeed.Name);
        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
    }

    [Fact]
    public async Task AnUnbanLiftsTheBan()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var member = await App.SeedAsync(new AccountSeed());
        using var ban = await admin.PostAsync($"{UsersPath}/{member.Id}/ban", new { });

        using var unban = await admin.PostAsync($"{UsersPath}/{member.Id}/unban");

        Assert.Equal(HttpStatusCode.NoContent, unban.StatusCode);
        var stored = await App.FindAsync(AccountSeed.Name);
        Assert.Equal((false, null, null), (stored.IsBanned, stored.BannedAt, stored.BanReason));
        await LastAuditAsync(AuditAction.AdminUserUnban);
        using var memberBrowser = App.Browser();
        using var signIn = await memberBrowser.SignInAsync(AccountSeed.Name);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task ADeletionRemovesTheAccountAndItsKeysAtOnce()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var member = await App.SeedAsync(new AccountSeed());
        var (_, secret) = await AdminSeed.ApiKeyAsync(App, member.Id);
        using var client = App.App();
        client.DefaultRequestHeaders.Add("X-Api-Key", secret);
        using var before = await client.GetAsync(Usage, Cancellation);

        using var deletion = await AdminCalls.DeleteAsync(admin, $"{UsersPath}/{member.Id}");

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        using var after = await client.GetAsync(Usage, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Null(await App.UsersAsync(users => users.FindByNameAsync(AccountSeed.Name)));
        var entry = await LastAuditAsync(AuditAction.AdminUserDelete);
        Assert.Equal(AccountSeed.Name, entry.Target);
    }

    [Fact]
    public async Task AnAdministratorNeverModeratesTheirOwnAccount()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var self = await AdminCalls.IdOfAsync(App, AdminBrowser.AdminName);

        using var ban = await admin.PostAsync($"{UsersPath}/{self}/ban", new { });
        using var deletion = await AdminCalls.DeleteAsync(admin, $"{UsersPath}/{self}");

        Assert.Equal(
            "self-moderation",
            await ApiJson.ProblemCodeAsync(ban, HttpStatusCode.Conflict));
        Assert.Equal(
            "self-moderation",
            await ApiJson.ProblemCodeAsync(deletion, HttpStatusCode.Conflict));
        Assert.False((await App.FindAsync(AdminBrowser.AdminName)).IsBanned);
    }

    [Fact]
    public async Task InvalidRequestsChangeNothing()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var member = await App.SeedAsync(new AccountSeed());

        using var unknown = await admin.PostAsync($"{UsersPath}/999999/ban", new { });
        using var tooLong = await admin.PostAsync(
            $"{UsersPath}/{member.Id}/ban",
            new { reason = new string('x', 256) });

        Assert.Equal(
            "user-not-found",
            await ApiJson.ProblemCodeAsync(unknown, HttpStatusCode.NotFound));
        Assert.Equal(["too-long"], (await ApiJson.FieldErrorsAsync(tooLong))["reason"]);
        Assert.False((await App.FindAsync(AccountSeed.Name)).IsBanned);
    }
}
