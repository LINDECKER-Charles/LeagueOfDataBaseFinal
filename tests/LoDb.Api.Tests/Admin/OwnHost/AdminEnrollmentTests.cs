using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin.OwnHost;

/// <summary>
/// <c>/api/admin/mfa</c>: an administrator without a second factor, holding the invitation
/// the operator printed, draws a key, confirms it with a code, and the session then holds the
/// second factor the admin asks for. Without the invitation, the password opens nothing. The
/// meters of the security count both.
/// </summary>
/// <remarks>A host per test: each reads the meters.</remarks>
public sealed class AdminEnrollmentTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres)
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
        var code = Totp.Code(key, App.Clock);
        // As an authenticator app shows it, in two groups.
        using var confirm = await admin.PostAsync(
            ConfirmPath,
            new { code = $"{code[..3]} {code[3..]}", invitation });

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
        var stored = await App.FindAsync(AdminCalls.NewAdminName);
        Assert.Equal((0, false), (stored.AccessFailedCount, stored.TwoFactorEnabled));
        var refusals = events.GetMeasurementSnapshot();
        Assert.Equal(3, refusals.Count);
        Assert.All(
            refusals,
            static refused => Assert.Equal(
                SecurityMetrics.MfaInvitationRefused,
                refused.Tags[SecurityMetrics.EventTag]));
    }

    private static async Task<JsonElement> StartAsync(BrowserClient admin, string invitation)
    {
        using var response = await admin.PostAsync(EnrollmentPath, new { invitation });
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }
}
