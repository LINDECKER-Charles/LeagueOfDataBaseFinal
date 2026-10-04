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
    : AdminTestBase(postgres), IClassFixture<PostgresContainerFixture>
{
    private const string Username = "root_admin";
    private const string Email = "root_admin@staging.example.test";

    // Operators' secrets: long, without the special character a member's password needs.
    private const string Secret = "OperatorSecretWithoutSymbols2026";
    private const string NewSecret = "AnotherOperatorSecretFor2027";

    [Fact]
    public void ApiDeclaresTheCommand()
    {
        var catalog = CliCommandCatalog.Discover(typeof(Program).Assembly);

        var command = Assert.Single(catalog.Commands, static c => c.Name == "admin root");
        Assert.True(command.RequiresHost);
    }

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

    // Arguments separated by one space, "-" for none; then the standard input.
    [Theory]
    [InlineData("-", Secret)]
    [InlineData("--username root_admin", Secret)]
    [InlineData("--email root_admin@staging.example.test", Secret)]
    [InlineData("--username ra --email root_admin@staging.example.test", Secret)]
    [InlineData("--username root_admin --email not-an-email", Secret)]
    [InlineData("--username root_admin --email a@b.test --password x", Secret)]
    [InlineData("--username root_admin --email root_admin@staging.example.test", "Sh0rt-pass")]
    [InlineData("--username root_admin --email root_admin@staging.example.test", "")]
    public async Task InvalidUsesCreateNothing(string line, string input)
    {
        var arguments = line == "-" ? [] : line.Split(' ');

        var (exit, _) = await AdminShell.RunAsync(App, input, ["admin", "root", .. arguments]);

        Assert.Equal(CliExitCodes.Usage, exit);
        Assert.Equal(["0"], await App.QueryAsync("SELECT count(*)::text FROM users"));
    }

    private Task<(int Exit, string Output)> RunAsync(string password) =>
        AdminShell.RunAsync(
            App,
            password + "\n",
            ["admin", "root", "--username", Username, $"--email={Email}"]);
}
