using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// A field of a sign-in past its bound, on the browser's sign-in and on the apps': the
/// request is refused as malformed before the account is looked up, so never hashed. The
/// bounds themselves are <c>Units/SignInFieldShapeTests</c>'.
/// </summary>
public sealed class SignInFieldBoundsTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private const string LoginPath = "/api/account/login";
    private const string TokenPath = "/api/account/token";
    private const string WaitHistogram = "lodb.hashing.wait";
    private const string Identifier = "identifier";
    private const string RecoveryCode = "recoveryCode";

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    public static TheoryData<string, string, string?, string> OutOfBounds => new()
    {
        { LoginPath, new string('a', 255), null, Identifier },
        { TokenPath, AccountSeed.Name, "AB3CD;EF4GH", RecoveryCode },
    };

    [Theory]
    [MemberData(nameof(OutOfBounds))]
    public async Task FieldPastItsBoundIsRefusedBeforeAnyHash(
        string path,
        string identifier,
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
            recoveryCode,
        });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(field, Assert.Single(errors).Key);
        Assert.Equal(["malformed"], errors[field]);
        Assert.Empty(waits.GetMeasurementSnapshot());
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
