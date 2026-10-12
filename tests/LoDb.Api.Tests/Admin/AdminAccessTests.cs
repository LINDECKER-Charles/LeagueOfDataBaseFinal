using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// Who opens the admin API: an administrator whose session holds the second factor, and no
/// one else; one without a second factor only reaches the enrollment.
/// </summary>
/// <remarks>The tests share a host, each with an administrator and a member of its own.</remarks>
public sealed class AdminAccessTests(SharedAccountsApp host)
    : AdminTestBase(host), IClassFixture<SharedAccountsApp>
{
    private static readonly string[] Panels =
    [
        "/api/admin/users",
        "/api/admin/builds",
        "/api/admin/contacts",
        "/api/admin/api-clients",
        "/api/admin/donations",
        "/api/admin/monitoring",
        "/api/admin/storage",
        "/api/admin/backups",
    ];

    private readonly AccountSeed _admin = host.Unique();
    private readonly AccountSeed _member = host.Unique();

    [Fact]
    public async Task AnonymousVisitorsMustSignIn()
    {
        using var visitor = App.Browser();

        foreach (var panel in Panels)
        {
            using var response = await visitor.GetAsync(panel);
            Assert.Equal(
                "authentication-required",
                await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Unauthorized));
        }
    }

    [Fact]
    public async Task MembersAreForbidden()
    {
        await App.SeedAsync(_member);
        using var member = await AdminCalls.SignedInAsync(App, _member.Username);

        foreach (var panel in Panels)
        {
            using var response = await member.GetAsync(panel);
            Assert.Equal(
                "forbidden",
                await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        }

        using var enrollment = await member.PostAsync("/api/admin/mfa/enrollment");
        Assert.Equal(
            "forbidden",
            await ApiJson.ProblemCodeAsync(enrollment, HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task AdministratorsWithoutSecondFactorMustEnrolFirst()
    {
        using var admin = await AdminCalls.OpenUnenrolledAsync(App, _admin);

        foreach (var panel in Panels)
        {
            using var response = await admin.GetAsync(panel);
            Assert.Equal(
                "mfa-required",
                await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        }
    }

    [Fact]
    public async Task PanelsOpenToAnAdministratorWithSecondFactorAndAreNeverCached()
    {
        using var admin = await AdminCalls.OpenEnrolledAsync(App, _admin);

        foreach (var panel in Panels)
        {
            using var response = await admin.GetAsync(panel);
            await ApiJson.ReadAsync(response, HttpStatusCode.OK);
            Assert.True(response.Headers.CacheControl?.NoStore, panel);
        }
    }

    [Fact]
    public async Task ActionsNeedTheAntiForgeryToken()
    {
        using var admin = await AdminCalls.OpenEnrolledAsync(App, _admin);
        var member = await App.SeedAsync(_member);
        using var forged = admin.Post($"/api/admin/users/{member.Id}/ban");
        forged.Headers.Remove(BrowserClient.XsrfHeader);

        using var response = await admin.SendAsync(forged);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False((await App.FindAsync(_member.Username)).IsBanned);
    }
}
