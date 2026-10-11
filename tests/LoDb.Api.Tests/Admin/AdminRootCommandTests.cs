using System.Net;
using LoDb.Api.Cli;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Testing;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>admin root</c>, as the deploy job runs it from the host's .env: the root administrator
/// created, kept as is, given a new password, and the uses it refuses.
/// </summary>
[Collection(ShellCommandsGroup.Name)]
public sealed class AdminRootCommandTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres)
{
    private const string Username = "root_admin";
    private const string Email = "root_admin@staging.example.test";

    // Operators' secrets: long, without the special character a member's password needs.
    private const string Secret = "OperatorSecretWithoutSymbols2026";
    private const string NewSecret = "AnotherOperatorSecretFor2027";

    [Fact]
    public async Task TheRootAdministratorIsCreatedAndSignsInByUsername()
    {
        var (exit, output) = await RunAsync(Secret);

        Assert.Equal(CliExitCodes.Success, exit);
        Assert.Contains(
            $"Created the root administrator {Username}.",
            output,
            StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, output, StringComparison.Ordinal);
        var root = await App.UsersAsync(users => users.FindByEmailAsync(Email));
        Assert.NotNull(root);
        Assert.Equal((Username, true), (root.UserName, root.EmailConfirmed));
        Assert.True(await App.UsersAsync(users => users.IsInRoleAsync(root, Role.Admin)));

        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(Username, Secret);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        using var locked = await browser.GetAsync("/api/admin/users");
        Assert.Equal(
            "mfa-required",
            await ApiJson.ProblemCodeAsync(locked, HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task ADeploymentWithTheSamePasswordChangesNothing()
    {
        await RunAsync(Secret);
        var first = await App.UsersAsync(users => users.FindByEmailAsync(Email));

        var (exit, output) = await RunAsync(Secret);

        Assert.Equal(CliExitCodes.Success, exit);
        Assert.Contains("already is the root administrator", output, StringComparison.Ordinal);
        var again = await App.UsersAsync(users => users.FindByEmailAsync(Email));
        Assert.Equal(
            (first!.PasswordHash, first.SecurityStamp),
            (again!.PasswordHash, again.SecurityStamp));
    }

    [Fact]
    public async Task ANewPasswordInTheEnvironmentReplacesTheOldOne()
    {
        await RunAsync(Secret);

        var (exit, output) = await RunAsync(NewSecret);

        Assert.Equal(CliExitCodes.Success, exit);
        Assert.Contains(
            "Set the password of the root administrator",
            output,
            StringComparison.Ordinal);
        using var browser = App.Browser();
        using var old = await browser.SignInAsync(Username, Secret);
        Assert.NotEqual(HttpStatusCode.OK, old.StatusCode);
        using var signIn = await browser.SignInAsync(Username, NewSecret);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task TheAccountOfTheEmailIsPromotedAndTakesThePassword()
    {
        var member = await App.SeedAsync(new AccountSeed { Username = Username, Email = Email });

        var (exit, _) = await RunAsync(Secret);

        Assert.Equal(CliExitCodes.Success, exit);
        Assert.True(await App.UsersAsync(users => users.IsInRoleAsync(member, Role.Admin)));
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(Email, Secret);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task AUsernameHeldByAnotherAccountStopsIt()
    {
        await App.SeedAsync(new AccountSeed { Username = Username });

        var (exit, _) = await RunAsync(Secret);

        Assert.Equal(CliExitCodes.Failure, exit);
        Assert.Null(await App.UsersAsync(users => users.FindByEmailAsync(Email)));
        Assert.Equal(["0"], await App.QueryAsync("SELECT count(*)::text FROM identity_user_roles"));
    }

    // The password read from the standard input; the other refusals are
    // AdminRootArgumentsTests'.
    [Fact]
    public async Task InvalidUseCreatesNothing()
    {
        var (exit, _) = await RunAsync("Sh0rt-pass");

        Assert.Equal(CliExitCodes.Usage, exit);
        Assert.Equal(["0"], await App.QueryAsync("SELECT count(*)::text FROM users"));
    }

    private Task<(int Exit, string Output)> RunAsync(string password) =>
        AdminShell.RunAsync(
            App,
            password + "\n",
            ["admin", "root", "--username", Username, $"--email={Email}"]);
}
