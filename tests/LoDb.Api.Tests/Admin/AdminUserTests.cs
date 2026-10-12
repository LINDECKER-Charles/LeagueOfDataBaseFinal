using System.Globalization;
using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>/api/admin/users</c>: the ban with its reason, the unban and the deletion, each recorded
/// in the audit journal. The search of the list is <see cref="OwnHost.AdminListsTests"/>'.
/// </summary>
/// <remarks>The tests share a host, each with an administrator and a member of its own.</remarks>
public sealed class AdminUserTests(SharedAccountsApp host)
    : AdminTestBase(host), IClassFixture<SharedAccountsApp>
{
    private const string UsersPath = "/api/admin/users";

    private static readonly Uri Usage = new("/v1/usage", UriKind.Relative);

    private readonly AccountSeed _admin = host.Unique();
    private readonly AccountSeed _member = host.Unique();

    [Fact]
    public async Task ABanClosesTheSessionAndIsAuditedWithItsReason()
    {
        using var admin = await AdminCalls.OpenEnrolledAsync(App, _admin);
        var member = await App.SeedAsync(_member);
        using var memberBrowser = App.Browser();
        using var signIn = await memberBrowser.SignInAsync(_member.Username);

        using var ban = await admin.PostAsync(
            $"{UsersPath}/{member.Id}/ban",
            new { reason = "  Spam de liens  " });

        Assert.Equal(HttpStatusCode.NoContent, ban.StatusCode);
        var stored = await App.FindAsync(_member.Username);
        Assert.Equal((true, "Spam de liens"), (stored.IsBanned, stored.BanReason));
        Assert.NotNull(stored.BannedAt);
        var entry = await LastAuditAsync(AuditAction.AdminUserBan);
        Assert.Equal(
            (AuditActorType.Admin, _admin.Username, AuditTargetType.User),
            (entry.ActorType, entry.Actor, entry.TargetType));
        Assert.Equal(member.Id.ToString(CultureInfo.InvariantCulture), entry.TargetId);
        Assert.Equal("Spam de liens", AccountsApp.Meta(entry, "reason"));
        Assert.Null(await memberBrowser.SignedInAsAsync());
        using var again = App.Browser();
        using var refused = await again.SignInAsync(_member.Username);
        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
    }

    [Fact]
    public async Task AnUnbanLiftsTheBan()
    {
        using var admin = await AdminCalls.OpenEnrolledAsync(App, _admin);
        var member = await App.SeedAsync(_member);
        using var ban = await admin.PostAsync($"{UsersPath}/{member.Id}/ban", new { });

        using var unban = await admin.PostAsync($"{UsersPath}/{member.Id}/unban");

        Assert.Equal(HttpStatusCode.NoContent, unban.StatusCode);
        var stored = await App.FindAsync(_member.Username);
        Assert.Equal((false, null, null), (stored.IsBanned, stored.BannedAt, stored.BanReason));
        await LastAuditAsync(AuditAction.AdminUserUnban);
        using var memberBrowser = App.Browser();
        using var signIn = await memberBrowser.SignInAsync(_member.Username);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task ADeletionRemovesTheAccountAndItsKeysAtOnce()
    {
        using var admin = await AdminCalls.OpenEnrolledAsync(App, _admin);
        var member = await App.SeedAsync(_member);
        var (_, secret) = await AdminSeed.ApiKeyAsync(App, member.Id);
        using var client = App.App();
        client.DefaultRequestHeaders.Add("X-Api-Key", secret);
        using var before = await client.GetAsync(Usage, Cancellation);

        using var deletion = await AdminCalls.DeleteAsync(admin, $"{UsersPath}/{member.Id}");

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        using var after = await client.GetAsync(Usage, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Null(await App.UsersAsync(users => users.FindByNameAsync(_member.Username)));
        var entry = await LastAuditAsync(AuditAction.AdminUserDelete);
        Assert.Equal(_member.Username, entry.Target);
    }

    [Fact]
    public async Task AnAdministratorNeverModeratesTheirOwnAccount()
    {
        using var admin = await AdminCalls.OpenEnrolledAsync(App, _admin);
        var self = await AdminCalls.IdOfAsync(App, _admin.Username);

        using var ban = await admin.PostAsync($"{UsersPath}/{self}/ban", new { });
        using var deletion = await AdminCalls.DeleteAsync(admin, $"{UsersPath}/{self}");

        Assert.Equal(
            "self-moderation",
            await ApiJson.ProblemCodeAsync(ban, HttpStatusCode.Conflict));
        Assert.Equal(
            "self-moderation",
            await ApiJson.ProblemCodeAsync(deletion, HttpStatusCode.Conflict));
        Assert.False((await App.FindAsync(_admin.Username)).IsBanned);
    }

    [Fact]
    public async Task InvalidRequestsChangeNothing()
    {
        using var admin = await AdminCalls.OpenEnrolledAsync(App, _admin);
        var member = await App.SeedAsync(_member);

        using var unknown = await admin.PostAsync($"{UsersPath}/999999/ban", new { });
        using var tooLong = await admin.PostAsync(
            $"{UsersPath}/{member.Id}/ban",
            new { reason = new string('x', 256) });

        Assert.Equal(
            "user-not-found",
            await ApiJson.ProblemCodeAsync(unknown, HttpStatusCode.NotFound));
        Assert.Equal(["too-long"], (await ApiJson.FieldErrorsAsync(tooLong))["reason"]);
        Assert.False((await App.FindAsync(_member.Username)).IsBanned);
    }
}
