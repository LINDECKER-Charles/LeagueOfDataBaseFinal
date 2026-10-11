using System.Net;
using LoDb.Api.Cli;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Api.Tests.Audit.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>admin mfa-invite</c> and <c>admin mfa-reset</c>, as an operator runs them in the API
/// container: the invitation that alone opens the enrolment of an authenticator, and the
/// removal of an authenticator whose device is lost, which closes the sessions.
/// </summary>
[Collection(ShellCommandsGroup.Name)]
public sealed class AdminMfaCommandTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres)
{
    private const string InvitationLine =
        "Invitation to enrol an authenticator, valid 15 minutes: ";
    private const string EnrollmentPath = "/api/admin/mfa/enrollment";
    private const string ConfirmPath = "/api/admin/mfa/confirm";

    [Theory]
    [InlineData("admin mfa-invite")]
    [InlineData("admin mfa-reset")]
    public void ApiDeclaresTheCommand(string name)
    {
        var catalog = CliCommandCatalog.Discover(typeof(Program).Assembly);

        var command = Assert.Single(catalog.Commands, c => c.Name == name);
        Assert.True(command.RequiresHost);
    }

    [Fact]
    public async Task TheInvitationLetsTheAdministratorEnrol()
    {
        using var admin = await AdminCalls.OpenUnenrolledAsync(App);

        var (exit, output) = await RunAsync("mfa-invite", AdminCalls.NewAdminEmail);

        Assert.Equal(CliExitCodes.Success, exit);
        var invitation = AdminShell.ValueOf(output, InvitationLine);
        Assert.Contains($"/admin/enroll?invitation={invitation}", output, StringComparison.Ordinal);
        await EnrolAsync(admin, invitation);
        await ReadAsync(admin, "/api/admin/users");
    }

    [Fact]
    public async Task OnlyAnAdministratorWithoutAuthenticatorIsInvited()
    {
        await App.SeedAsync(new AccountSeed());
        using var enrolled = await AdminBrowser.OpenAsync(App);

        var (member, _) = await RunAsync("mfa-invite", AccountSeed.Address);
        var (unknown, _) = await RunAsync("mfa-invite", "nobody@example.test");
        var (alreadyEnrolled, output) = await RunAsync("mfa-invite", AdminBrowser.AdminEmail);
        var (usage, _) = await AdminShell.RunAsync(App, string.Empty, ["admin", "mfa-invite"]);

        Assert.Equal(
            (CliExitCodes.Failure, CliExitCodes.Failure, CliExitCodes.Failure, CliExitCodes.Usage),
            (member, unknown, alreadyEnrolled, usage));
        Assert.DoesNotContain(InvitationLine, output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheResetRemovesTheAuthenticatorAndClosesTheSessions()
    {
        using var admin = await AdminBrowser.OpenAsync(App);

        var (exit, output) = await RunAsync("mfa-reset", AdminBrowser.AdminEmail);
        App.Clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(CliExitCodes.Success, exit);
        Assert.Contains("admin mfa-invite --email", output, StringComparison.Ordinal);
        var stored = await App.FindAsync(AdminBrowser.AdminName);
        Assert.False(stored.TwoFactorEnabled);
        Assert.Equal(0, await App.UsersAsync(users => users.CountRecoveryCodesAsync(stored)));
        Assert.Null(await admin.SignedInAsAsync());
        var (invited, _) = await RunAsync("mfa-invite", AdminBrowser.AdminEmail);
        Assert.Equal(CliExitCodes.Success, invited);
    }

    [Fact]
    public async Task TheResetOfAnUnknownAccountChangesNothing()
    {
        var (exit, _) = await RunAsync("mfa-reset", "nobody@example.test");

        Assert.Equal(CliExitCodes.Failure, exit);
    }

    private async Task EnrolAsync(BrowserClient admin, string invitation)
    {
        using var start = await admin.PostAsync(EnrollmentPath, new { invitation });
        var key = ApiJson.Text(await ApiJson.ReadAsync(start, HttpStatusCode.OK), "sharedKey")!;
        using var confirm = await admin.PostAsync(
            ConfirmPath,
            new { code = Totp.Code(key, App.Clock), invitation });
        await ApiJson.ReadAsync(confirm, HttpStatusCode.OK);
    }

    private Task<(int Exit, string Output)> RunAsync(string command, string email) =>
        AdminShell.RunAsync(App, string.Empty, ["admin", command, "--email", email]);
}
