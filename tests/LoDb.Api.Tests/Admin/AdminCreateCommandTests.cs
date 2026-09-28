using System.Globalization;
using System.Net;
using LoDb.Api.Cli;
using LoDb.Api.Modules.Accounts;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Admin.Support;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Admin;

/// <summary>
/// <c>admin create --email</c>, as an operator runs it in the API container: the first
/// administrator created, an account promoted, and the enrollment that then opens the admin.
/// </summary>
public sealed class AdminCreateCommandTests(PostgresContainerFixture postgres)
    : AdminTestBase(postgres), IClassFixture<PostgresContainerFixture>
{
    private const string FirstEmail = "first.admin@example.test";
    private const string PasswordLine = "Password, shown only now: ";

    [Fact]
    public void ApiDeclaresTheCommand()
    {
        var catalog = CliCommandCatalog.Discover(typeof(Program).Assembly);

        var command = Assert.Single(catalog.Commands, static c => c.Name == "admin create");
        Assert.True(command.RequiresHost);
    }

    [Fact]
    public async Task TheFirstAdministratorIsCreatedThenEnrols()
    {
        var (exit, output) = await RunAsync("--email", "  First.Admin@Example.TEST ");

        Assert.Equal(CliExitCodes.Success, exit);
        var created = await App.UsersAsync(users => users.FindByEmailAsync(FirstEmail));
        Assert.NotNull(created);
        Assert.Equal(
            ("first-admin", true, false),
            (created.UserName, created.EmailConfirmed, created.TwoFactorEnabled));
        Assert.True(await App.UsersAsync(users => users.IsInRoleAsync(created, Role.Admin)));
        Assert.Contains(
            $"Created the administrator account first-admin <{FirstEmail}>.",
            output,
            StringComparison.Ordinal);
        Assert.Contains("Sign in at /admin/login", output, StringComparison.Ordinal);

        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(FirstEmail, PasswordOf(output));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        using var locked = await browser.GetAsync("/api/admin/users");
        Assert.Equal(
            "mfa-required",
            await ApiJson.ProblemCodeAsync(locked, HttpStatusCode.Forbidden));
        using var start = await browser.PostAsync("/api/admin/mfa/enrollment");
        var key = ApiJson.Text(await ApiJson.ReadAsync(start, HttpStatusCode.OK), "sharedKey")!;
        using var confirm = await browser.PostAsync(
            "/api/admin/mfa/confirm",
            new { code = Totp.Code(key) });
        await ApiJson.ReadAsync(confirm, HttpStatusCode.OK);
        await ReadAsync(browser, "/api/admin/users");
    }

    [Fact]
    public async Task AnExistingAccountIsPromotedAndKeepsItsPassword()
    {
        var member = await App.SeedAsync(new AccountSeed());

        var (exit, output) = await RunAsync("--email", AccountSeed.Address);

        Assert.Equal(CliExitCodes.Success, exit);
        Assert.Contains(
            $"Made {AccountSeed.Name} <{AccountSeed.Address}> an administrator.",
            output,
            StringComparison.Ordinal);
        Assert.DoesNotContain(PasswordLine, output, StringComparison.Ordinal);
        Assert.True(await App.UsersAsync(users => users.IsInRoleAsync(member, Role.Admin)));
        using var browser = App.Browser();
        using var signIn = await browser.SignInAsync(AccountSeed.Name);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task RunningItAgainChangesNothing()
    {
        await RunAsync("--email", FirstEmail);
        var first = await App.UsersAsync(users => users.FindByEmailAsync(FirstEmail));

        var (exit, output) = await RunAsync($"--email={FirstEmail}");

        Assert.Equal(CliExitCodes.Success, exit);
        Assert.Contains("already is an administrator", output, StringComparison.Ordinal);
        Assert.DoesNotContain(PasswordLine, output, StringComparison.Ordinal);
        var again = await App.UsersAsync(users => users.FindByEmailAsync(FirstEmail));
        Assert.Equal(first!.PasswordHash, again!.PasswordHash);
        Assert.Equal(["1"], await App.QueryAsync("SELECT count(*)::text FROM identity_roles"));
        Assert.Equal(["1"], await App.QueryAsync("SELECT count(*)::text FROM identity_user_roles"));
    }

    // Arguments separated by one space; "-" for none.
    [Theory]
    [InlineData("-")]
    [InlineData("--email")]
    [InlineData("--email not-an-email")]
    [InlineData("--email a@example.test --email b@example.test")]
    [InlineData("--mail a@example.test")]
    public async Task InvalidArgumentsCreateNothing(string line)
    {
        var (exit, _) = await RunAsync(line == "-" ? [] : line.Split(' '));

        Assert.Equal(CliExitCodes.Usage, exit);
        Assert.Equal(["0"], await App.QueryAsync("SELECT count(*)::text FROM users"));
    }

    private static string PasswordOf(string output) =>
        output.Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .Single(static line => line.StartsWith(PasswordLine, StringComparison.Ordinal))
            [PasswordLine.Length..];

    // The command prints on the standard output, captured for the run.
    private async Task<(int Exit, string Output)> RunAsync(params string[] arguments)
    {
        var connection = App.Services.GetRequiredService<IConfiguration>()
            .GetConnectionString("LoDb");
        var original = Console.Out;
        await using var output = new StringWriter(CultureInfo.InvariantCulture);
        Console.SetOut(output);
        try
        {
            var exit = await CliRunner.RunAsync(
                [
                    "admin", "create",
                    $"--ConnectionStrings:LoDb={connection}",
                    "--Logging:LogLevel:Default=Warning",
                    .. arguments,
                ],
                typeof(Program).Assembly,
                static (services, configuration) =>
                    services.AddLoDbPersistence(configuration).AddAccounts(configuration));
            return (exit, output.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
