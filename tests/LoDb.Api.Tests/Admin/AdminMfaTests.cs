using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>/api/admin/mfa</c>: an administrator without a second factor, holding the invitation
/// the operator printed, draws a key, confirms it with a code, and the session then holds the
/// second factor the admin asks for. Without the invitation, the password opens nothing.
/// </summary>
public sealed class AdminMfaTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres), IClassFixture<PostgresContainerFixture>
{
    private const string EnrollmentPath = "/api/admin/mfa/enrollment";
    private const string ConfirmPath = "/api/admin/mfa/confirm";
    private const string EventsCounter = "lodb.admin.security_events";
    private const string InvitationField = "invitation";

    [Fact]
    public async Task EnrollmentOpensTheAdmin()
    {
        using var events = App.Security<long>(EventsCounter);
        using var admin = await AdminCalls.OpenUnenrolledAsync(App);
        var invitation = await AdminCalls.InvitationAsync(App, AdminCalls.NewAdminName);

        var setup = await StartAsync(admin, invitation);
        var key = ApiJson.Text(setup, "sharedKey")!;
        using var confirm = await admin.PostAsync(
            ConfirmPath,
            new { code = Totp.Code(key), invitation });

        var done = await ApiJson.ReadAsync(confirm, HttpStatusCode.OK);
        Assert.Equal(10, done.GetProperty("recoveryCodes").GetArrayLength());
        var user = done.GetProperty("session").GetProperty("user");
        Assert.True(user.GetProperty("multiFactor").GetBoolean());
        Assert.True(user.GetProperty("twoFactorEnabled").GetBoolean());
        Assert.StartsWith(
            "otpauth://totp/LoDb:nouvelle.admin%40example.test?secret=" + key,
            ApiJson.Text(setup, "authenticatorUri"),
            StringComparison.Ordinal);
        await ReadAsync(admin, "/api/admin/users");
        Assert.True((await App.FindAsync(AdminCalls.NewAdminName)).TwoFactorEnabled);
        var enrolled = Assert.Single(events.GetMeasurementSnapshot());
        Assert.Equal(SecurityMetrics.MfaEnrolled, enrolled.Tags[SecurityMetrics.EventTag]);
    }

    [Fact]
    public async Task ThePasswordAloneEnrolsNothing()
    {
        using var events = App.Security<long>(EventsCounter);
        using var admin = await AdminCalls.OpenUnenrolledAsync(App);

        using var missing = await admin.PostAsync(EnrollmentPath, new { });
        using var forged = await admin.PostAsync(EnrollmentPath, new { invitation = "forged" });
        using var confirm = await admin.PostAsync(ConfirmPath, new { code = "123456" });

        Assert.Equal(["required"], (await ApiJson.FieldErrorsAsync(missing))[InvitationField]);
        Assert.Equal(
            ["invalid-invitation"],
            (await ApiJson.FieldErrorsAsync(forged))[InvitationField]);
        Assert.Equal(["required"], (await ApiJson.FieldErrorsAsync(confirm))[InvitationField]);
        Assert.Null(await App.UsersAsync(async users =>
            await users.GetAuthenticatorKeyAsync(
                (await users.FindByNameAsync(AdminCalls.NewAdminName))!)));
        Assert.Equal((0, false), await FailuresAsync());
        var refusals = events.GetMeasurementSnapshot();
        Assert.Equal(3, refusals.Count);
        Assert.All(
            refusals,
            static refused => Assert.Equal(
                SecurityMetrics.MfaInvitationRefused,
                refused.Tags[SecurityMetrics.EventTag]));
    }

    [Fact]
    public async Task AnInvitationServesOneAccountForAFewMinutes()
    {
        await App.SeedAsync(new AccountSeed());
        var other = await AdminCalls.InvitationAsync(App, AccountSeed.Name);
        using var admin = await AdminCalls.OpenUnenrolledAsync(App);
        var invitation = await AdminCalls.InvitationAsync(App, AdminCalls.NewAdminName);

        using var someoneElses = await admin.PostAsync(EnrollmentPath, new { invitation = other });
        App.Clock.Advance(TimeSpan.FromMinutes(16));
        using var expired = await admin.PostAsync(EnrollmentPath, new { invitation });

        Assert.Equal(
            ["invalid-invitation"],
            (await ApiJson.FieldErrorsAsync(someoneElses))[InvitationField]);
        Assert.Equal(
            ["invalid-invitation"],
            (await ApiJson.FieldErrorsAsync(expired))[InvitationField]);
    }

    [Fact]
    public async Task AWrongCodeCountsTowardTheLockout()
    {
        using var admin = await AdminCalls.OpenUnenrolledAsync(App);
        var invitation = await AdminCalls.InvitationAsync(App, AdminCalls.NewAdminName);
        var key = ApiJson.Text(await StartAsync(admin, invitation), "sharedKey")!;

        using var wrong = await admin.PostAsync(
            ConfirmPath,
            new { code = Totp.WrongCode(key), invitation });
        using var blank = await admin.PostAsync(ConfirmPath, new { code = " ", invitation });
        using var malformed = await admin.PostAsync(
            ConfirmPath,
            new { code = "12345a", invitation });

        Assert.Equal(["invalid-code"], (await ApiJson.FieldErrorsAsync(wrong))["code"]);
        Assert.Equal(["required"], (await ApiJson.FieldErrorsAsync(blank))["code"]);
        Assert.Equal(["malformed"], (await ApiJson.FieldErrorsAsync(malformed))["code"]);
        Assert.Equal((1, false), await FailuresAsync());
        using var panel = await admin.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, panel.StatusCode);
    }

    [Fact]
    public async Task ABearerHeaderDoesNotExemptTheSessionFromItsXsrfToken()
    {
        using var admin = await AdminCalls.OpenUnenrolledAsync(App);
        var invitation = await AdminCalls.InvitationAsync(App, AdminCalls.NewAdminName);
        using var request = admin.Post(EnrollmentPath, new { invitation });
        request.Headers.Remove(BrowserClient.XsrfHeader);
        request.Headers.Authorization = new("Bearer", "forged");

        using var response = await admin.SendAsync(request);

        Assert.Equal(
            "xsrf-invalid",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        Assert.Null(await App.UsersAsync(async users =>
            await users.GetAuthenticatorKeyAsync(
                (await users.FindByNameAsync(AdminCalls.NewAdminName))!)));
    }

    [Fact]
    public async Task ACodeWithSeparatorsIsAccepted()
    {
        using var admin = await AdminCalls.OpenUnenrolledAsync(App);
        var invitation = await AdminCalls.InvitationAsync(App, AdminCalls.NewAdminName);
        var key = ApiJson.Text(await StartAsync(admin, invitation), "sharedKey")!;
        var code = Totp.Code(key);

        using var confirm = await admin.PostAsync(
            ConfirmPath,
            new { code = $"{code[..3]} {code[3..]}", invitation });

        await ApiJson.ReadAsync(confirm, HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnEnrolledAdministratorKeepsTheirAuthenticator()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var invitation = await AdminCalls.InvitationAsync(App, AdminBrowser.AdminName);
        var before = await App.UsersAsync(async users =>
            await users.GetAuthenticatorKeyAsync(
                (await users.FindByNameAsync(AdminBrowser.AdminName))!));

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
        var after = await App.UsersAsync(async users =>
            await users.GetAuthenticatorKeyAsync(
                (await users.FindByNameAsync(AdminBrowser.AdminName))!));
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task TheNextSignInAsksForTheCode()
    {
        using (var admin = await AdminCalls.OpenUnenrolledAsync(App))
        {
            var invitation = await AdminCalls.InvitationAsync(App, AdminCalls.NewAdminName);
            var key = ApiJson.Text(await StartAsync(admin, invitation), "sharedKey")!;
            using var confirm = await admin.PostAsync(
                ConfirmPath,
                new { code = Totp.Code(key), invitation });
            await ApiJson.ReadAsync(confirm, HttpStatusCode.OK);
        }

        using var browser = App.Browser();
        using var password = await browser.SignInAsync(AdminCalls.NewAdminName);

        Assert.NotEqual(HttpStatusCode.OK, password.StatusCode);
        Assert.Null(await browser.SignedInAsAsync());
    }

    private static async Task<JsonElement> StartAsync(BrowserClient admin, string invitation)
    {
        using var response = await admin.PostAsync(EnrollmentPath, new { invitation });
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    private async Task<(int Failures, bool Enrolled)> FailuresAsync()
    {
        var stored = await App.FindAsync(AdminCalls.NewAdminName);
        return (stored.AccessFailedCount, stored.TwoFactorEnabled);
    }
}
