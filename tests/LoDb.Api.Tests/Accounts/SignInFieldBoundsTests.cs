using System.Net;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The fields of a sign-in past their bounds: an identifier longer than any e-mail, a code
/// that is not digits, a recovery code that is not letters and digits. The request is
/// refused as malformed before the account is looked up, so never hashed.
/// </summary>
public sealed class SignInFieldBoundsTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string LoginPath = "/api/account/login";
    private const string TokenPath = "/api/account/token";
    private const string WaitHistogram = "lodb.hashing.wait";
    private const string Identifier = "identifier";
    private const string TwoFactorCode = "twoFactorCode";
    private const string RecoveryCode = "recoveryCode";

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    public static TheoryData<string, string, string?, string?, string> OutOfBounds => new()
    {
        { LoginPath, new string('a', 255), null, null, Identifier },
        { TokenPath, new string('a', 255), null, null, Identifier },
        { LoginPath, AccountSeed.Name, "12345678901234567", null, TwoFactorCode },
        { LoginPath, AccountSeed.Name, "12345a", null, TwoFactorCode },
        { TokenPath, AccountSeed.Name, "<script>", null, TwoFactorCode },
        { LoginPath, AccountSeed.Name, null, new string('A', 33), RecoveryCode },
        { TokenPath, AccountSeed.Name, null, "AB3CD;EF4GH", RecoveryCode },
    };

    [Theory]
    [MemberData(nameof(OutOfBounds))]
    public async Task FieldPastItsBoundIsRefusedBeforeAnyHash(
        string path,
        string identifier,
        string? twoFactorCode,
        string? recoveryCode,
        string field)
    {
        await App.SeedAsync(new AccountSeed());
        using var waits = App.Security<double>(WaitHistogram);
        using var browser = App.Browser();

        using var response = await browser.PostAsync(path, new
        {
            identifier,
            password = AccountSeed.StrongPassword,
            twoFactorCode,
            recoveryCode,
        });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(field, Assert.Single(errors).Key);
        Assert.Equal(["malformed"], errors[field]);
        Assert.Empty(waits.GetMeasurementSnapshot());
    }

    [Theory]
    [InlineData("123 456", null)]
    [InlineData("123-456", null)]
    [InlineData(null, "ab3cd-ef4gh")]
    public async Task CodeWithinItsBoundIsChecked(string? twoFactorCode, string? recoveryCode)
    {
        await App.SeedAsync(new AccountSeed());
        using var browser = App.Browser();

        using var response = await browser.PostAsync(LoginPath, new
        {
            identifier = AccountSeed.Name,
            password = AccountSeed.StrongPassword,
            twoFactorCode,
            recoveryCode,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
