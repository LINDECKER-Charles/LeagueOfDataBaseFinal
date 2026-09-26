using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>/api/admin/mfa</c>: an administrator without a second factor draws a key, confirms it
/// with a code, and the session then holds the second factor the admin asks for.
/// </summary>
public sealed class AdminMfaTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres), IClassFixture<PostgresContainerFixture>
{
    private const string EnrollmentPath = "/api/admin/mfa/enrollment";
    private const string ConfirmPath = "/api/admin/mfa/confirm";

    [Fact]
    public async Task EnrollmentOpensTheAdmin()
    {
        using var admin = await AdminCalls.OpenUnenrolledAsync(App);

        var setup = await StartAsync(admin);
        var key = ApiJson.Text(setup, "sharedKey")!;
        using var confirm = await admin.PostAsync(ConfirmPath, new { code = Totp.Code(key) });

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
    }

    [Fact]
    public async Task AWrongCodeCountsTowardTheLockout()
    {
        using var admin = await AdminCalls.OpenUnenrolledAsync(App);
        var key = ApiJson.Text(await StartAsync(admin), "sharedKey")!;

        using var wrong = await admin.PostAsync(ConfirmPath, new { code = Totp.WrongCode(key) });
        using var blank = await admin.PostAsync(ConfirmPath, new { code = " " });

        Assert.Equal(["invalid-code"], (await ApiJson.FieldErrorsAsync(wrong))["code"]);
        Assert.Equal(["required"], (await ApiJson.FieldErrorsAsync(blank))["code"]);
        var stored = await App.FindAsync(AdminCalls.NewAdminName);
        Assert.Equal((1, false), (stored.AccessFailedCount, stored.TwoFactorEnabled));
        using var panel = await admin.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, panel.StatusCode);
    }

    [Fact]
    public async Task ACodeWithSeparatorsIsAccepted()
    {
        using var admin = await AdminCalls.OpenUnenrolledAsync(App);
        var key = ApiJson.Text(await StartAsync(admin), "sharedKey")!;
        var code = Totp.Code(key);

        using var confirm = await admin.PostAsync(
            ConfirmPath,
            new { code = $"{code[..3]} {code[3..]}" });

        await ApiJson.ReadAsync(confirm, HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnEnrolledAdministratorKeepsTheirAuthenticator()
    {
        using var admin = await AdminBrowser.OpenAsync(App);
        var before = await App.UsersAsync(async users =>
            await users.GetAuthenticatorKeyAsync(
                (await users.FindByNameAsync(AdminBrowser.AdminName))!));

        using var start = await admin.PostAsync(EnrollmentPath);
        using var confirm = await admin.PostAsync(ConfirmPath, new { code = "123456" });

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
            var key = ApiJson.Text(await StartAsync(admin), "sharedKey")!;
            using var confirm = await admin.PostAsync(ConfirmPath, new { code = Totp.Code(key) });
            await ApiJson.ReadAsync(confirm, HttpStatusCode.OK);
        }

        using var browser = App.Browser();
        using var password = await browser.SignInAsync(AdminCalls.NewAdminName);

        Assert.NotEqual(HttpStatusCode.OK, password.StatusCode);
        Assert.Null(await browser.SignedInAsAsync());
    }

    private static async Task<System.Text.Json.JsonElement> StartAsync(BrowserClient admin)
    {
        using var response = await admin.PostAsync(EnrollmentPath);
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }
}
