using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// Who opens the admin API: an administrator whose session holds the second factor, and no
/// one else; one without a second factor only reaches the enrollment.
/// </summary>
public sealed class AdminAccessTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres), IClassFixture<PostgresContainerFixture>
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
    ];

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
        using var member = await AdminBrowser.OpenMemberAsync(App);

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
        using var admin = await AdminCalls.OpenUnenrolledAsync(App);

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
        using var admin = await AdminBrowser.OpenAsync(App);

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
        using var admin = await AdminBrowser.OpenAsync(App);
        var member = await App.SeedAsync(new AccountSeed());
        using var forged = admin.Post($"/api/admin/users/{member.Id}/ban");
        forged.Headers.Remove(BrowserClient.XsrfHeader);

        using var response = await admin.SendAsync(forged);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False((await App.FindAsync(AccountSeed.Name)).IsBanned);
    }
}
