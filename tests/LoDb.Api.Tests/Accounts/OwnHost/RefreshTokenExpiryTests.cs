using System.Net;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts.OwnHost;

/// <summary>
/// The refresh token of an app lasts thirty days, and <c>/refresh</c> exchanges it past the
/// five minutes of the bearer token until the account's stamp changes.
/// </summary>
/// <remarks>A host per test: each advances its clock.</remarks>
public sealed class RefreshTokenExpiryTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    [Fact]
    public async Task RefreshGivesNewTokensUntilTheStampChanges()
    {
        await App.SeedAsync(new AccountSeed());
        using var app = App.App();
        var tokens = await AppTokens.SignInAsync(app);

        App.Clock.Advance(TimeSpan.FromMinutes(10));
        var renewed = await AppTokens.RenewAsync(app, tokens);
        await App.RenewStampAsync(AccountSeed.Name);
        using var revoked = await AppTokens.RefreshAsync(app, renewed);

        Assert.Equal(AccountSeed.Name, await SignedInAsAsync(app, renewed));
        Assert.Equal(
            "invalid-refresh-token",
            await ApiJson.ProblemCodeAsync(revoked, HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task RefreshTokenLastsThirtyDays()
    {
        await App.SeedAsync(new AccountSeed());
        using var app = App.App();
        var tokens = await AppTokens.SignInAsync(app);

        App.Clock.Advance(TimeSpan.FromDays(31));
        using var expired = await AppTokens.RefreshAsync(app, tokens);

        Assert.Equal(
            "invalid-refresh-token",
            await ApiJson.ProblemCodeAsync(expired, HttpStatusCode.Unauthorized));
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static async Task<string?> SignedInAsAsync(HttpClient app, JsonElement tokens)
    {
        using var response = await AppTokens.SendAsync(app, HttpMethod.Get, "me", tokens);
        var user = (await ApiJson.ReadAsync(response, HttpStatusCode.OK)).GetProperty("user");
        return user.ValueKind == JsonValueKind.Null ? null : ApiJson.Text(user, "username");
    }
}
