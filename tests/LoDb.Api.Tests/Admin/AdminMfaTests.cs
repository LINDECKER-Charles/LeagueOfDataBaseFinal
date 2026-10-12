using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>/api/admin/mfa</c>: a wrong code counts toward the lockout, the session's XSRF token
/// stays required, and an enrolled administrator keeps their authenticator. The enrollment
/// itself, and the refusals counted, are <see cref="OwnHost.AdminEnrollmentTests"/>'.
/// </summary>
/// <remarks>The tests share a host, each with an administrator of its own.</remarks>
public sealed class AdminMfaTests(SharedAccountsApp host)
    : AdminTestBase(host), IClassFixture<SharedAccountsApp>
{
    private const string EnrollmentPath = "/api/admin/mfa/enrollment";
    private const string ConfirmPath = "/api/admin/mfa/confirm";

    private readonly AccountSeed _admin = host.Unique();

    [Fact]
    public async Task AWrongCodeCountsTowardTheLockout()
    {
        using var admin = await AdminCalls.OpenUnenrolledAsync(App, _admin);
        var invitation = await AdminCalls.InvitationAsync(App, _admin.Username);
        var key = ApiJson.Text(await StartAsync(admin, invitation), "sharedKey")!;

        using var wrong = await admin.PostAsync(
            ConfirmPath,
            new { code = Totp.WrongCode(key, App.Clock), invitation });
        using var blank = await admin.PostAsync(ConfirmPath, new { code = " ", invitation });
        using var malformed = await admin.PostAsync(
            ConfirmPath,
            new { code = "12345a", invitation });

        Assert.Equal(["invalid-code"], (await ApiJson.FieldErrorsAsync(wrong))["code"]);
        Assert.Equal(["required"], (await ApiJson.FieldErrorsAsync(blank))["code"]);
        Assert.Equal(["malformed"], (await ApiJson.FieldErrorsAsync(malformed))["code"]);
        var stored = await App.FindAsync(_admin.Username);
        Assert.Equal((1, false), (stored.AccessFailedCount, stored.TwoFactorEnabled));
        using var panel = await admin.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, panel.StatusCode);
    }

    [Fact]
    public async Task ABearerHeaderDoesNotExemptTheSessionFromItsXsrfToken()
    {
        using var admin = await AdminCalls.OpenUnenrolledAsync(App, _admin);
        var invitation = await AdminCalls.InvitationAsync(App, _admin.Username);
        using var request = admin.Post(EnrollmentPath, new { invitation });
        request.Headers.Remove(BrowserClient.XsrfHeader);
        request.Headers.Authorization = new("Bearer", "forged");

        using var response = await admin.SendAsync(request);

        Assert.Equal(
            "xsrf-invalid",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Null(await AuthenticatorKeyAsync());
    }

    [Fact]
    public async Task AnEnrolledAdministratorKeepsTheirAuthenticator()
    {
        using var admin = await AdminCalls.OpenEnrolledAsync(App, _admin);
        var invitation = await AdminCalls.InvitationAsync(App, _admin.Username);
        var before = await AuthenticatorKeyAsync();

        using var start = await admin.PostAsync(EnrollmentPath, new { invitation });
        using var confirm = await admin.PostAsync(
            ConfirmPath,
            new { code = "123456", invitation });

        Assert.Equal(
            "mfa-already-enrolled",
            await ApiJson.ProblemCodeAsync(start, HttpStatusCode.Conflict));
        Assert.Equal(
            "mfa-already-enrolled",
            await ApiJson.ProblemCodeAsync(confirm, HttpStatusCode.Conflict));
        Assert.Equal(before, await AuthenticatorKeyAsync());
    }

    private static async Task<JsonElement> StartAsync(BrowserClient admin, string invitation)
    {
        using var response = await admin.PostAsync(EnrollmentPath, new { invitation });
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    private Task<string?> AuthenticatorKeyAsync() =>
        App.UsersAsync(async users =>
            await users.GetAuthenticatorKeyAsync(
                (await users.FindByNameAsync(_admin.Username))!));
}
