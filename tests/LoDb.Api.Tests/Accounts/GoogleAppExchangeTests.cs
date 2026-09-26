using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// <c>POST /api/account/google/app/exchange</c>: an app hands over the code of its Google
/// sign-in with its PKCE verifier, redeemed with the client it names, and gets the tokens of
/// the account, under the same linking rules as the web.
/// </summary>
public sealed class GoogleAppExchangeTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string ExchangePath = "/api/account/google/app/exchange";
    private const string Subject = "110248495921238986420";
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string AndroidRedirect = "com.leagueofdatabase.app:/oauth2redirect";

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AndroidCodeGivesTheTokensOfTheAccount()
    {
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = Subject,
            Email = "Joueur@gmail.test",
            GivenName = "Joueur",
        };
        using var app = App.App();

        using var response = await ExchangeAsync(app, Exchange(AccountsApp.AndroidClientId));

        var tokens = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal("Bearer", ApiJson.Text(tokens, "tokenType"));
        Assert.False(string.IsNullOrEmpty(ApiJson.Text(tokens, "refreshToken")));
        Assert.Equal("Joueur", await SignedInAsAsync(app, tokens));
        var exchange = Assert.Single(App.GoogleServer.Exchanges);
        Assert.Equal(
            (AccountsApp.AndroidClientId, Verifier, AndroidRedirect, FakeGoogle.Code),
            (exchange["client_id"], exchange["code_verifier"], exchange["redirect_uri"],
                exchange["code"]));
        Assert.False(exchange.ContainsKey("client_secret"));
        var login = Assert.Single(await App.AuditAsync());
        Assert.Equal(
            (AuditAction.UserLogin, "google", "token"),
            (login.Action, AccountsApp.Meta(login, "method"), AccountsApp.Meta(login, "channel")));
    }

    [Theory]
    [InlineData(AccountsApp.DesktopClientId, AccountsApp.DesktopClientSecret)]
    [InlineData(null, AccountsApp.WebClientSecret)]
    public async Task CodeIsRedeemedWithTheSecretOfItsClient(string? clientId, string secret)
    {
        App.GoogleServer.Account = new GoogleAccount { Subject = Subject, Email = "a@gmail.test" };
        using var app = App.App();

        using var response = await ExchangeAsync(app, Exchange(clientId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var exchange = Assert.Single(App.GoogleServer.Exchanges);
        Assert.Equal(
            (clientId ?? AccountsApp.WebClientId, secret),
            (exchange["client_id"], exchange["client_secret"]));
    }

    [Fact]
    public async Task UnknownClientAndMissingFieldsAreReportedTogether()
    {
        using var app = App.App();

        using var response = await ExchangeAsync(app, new
        {
            code = " ",
            codeVerifier = (string?)null,
            redirectUri = string.Empty,
            clientId = "other.apps.googleusercontent.com",
        });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["required"], errors["code"]);
        Assert.Equal(["required"], errors["codeVerifier"]);
        Assert.Equal(["required"], errors["redirectUri"]);
        Assert.Equal(["unknown-client"], errors["clientId"]);
        Assert.Empty(App.GoogleServer.Exchanges);
    }

    [Fact]
    public async Task CodeGoogleRefusesGivesNoToken()
    {
        App.GoogleServer.Account = new GoogleAccount { Subject = Subject, Email = "a@gmail.test" };
        using var app = App.App();

        using var response = await ExchangeAsync(app, Exchange(code: "stolen"));

        Assert.Equal(
            "google-exchange-failed",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.BadRequest));
        Assert.Equal(["0"], await App.QueryAsync("SELECT count(*) FROM users"));
    }

    [Fact]
    public async Task UnverifiedEmailNeverTakesTheAccountThatUsesIt()
    {
        await App.SeedAsync(new AccountSeed());
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = Subject,
            Email = AccountSeed.Address,
            EmailVerified = false,
        };
        using var app = App.App();

        using var response = await ExchangeAsync(app, Exchange());

        Assert.Equal(
            "google-email-unverified",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Conflict));
        Assert.Null((await App.FindAsync(AccountSeed.Name)).GoogleId);
    }

    [Fact]
    public async Task BannedAccountGetsNoToken()
    {
        await App.SeedAsync(new AccountSeed { GoogleId = Subject, Banned = true });
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = Subject,
            Email = AccountSeed.Address,
        };
        using var app = App.App();

        using var response = await ExchangeAsync(app, Exchange());

        Assert.Equal(
            "account-banned",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        var refusal = Assert.Single(await App.AuditAsync());
        Assert.Equal(AuditAction.UserLoginFailed, refusal.Action);
    }

    [Fact]
    public async Task UnreachableGoogleIsUnavailable()
    {
        App.GoogleServer.Unreachable = true;
        using var app = App.App();

        using var response = await ExchangeAsync(app, Exchange());

        Assert.Equal(
            "google-unavailable",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.ServiceUnavailable));
    }

    [Fact]
    public async Task WithoutAClientIdGoogleIsUnavailable()
    {
        await using var withoutGoogle = await AccountsApp.StartAsync(postgres, withGoogle: false);
        using var app = withoutGoogle.App();

        using var response = await ExchangeAsync(app, Exchange());

        Assert.Equal(
            "google-unavailable",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.ServiceUnavailable));
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static object Exchange(string? clientId = null, string code = FakeGoogle.Code) =>
        new { code, codeVerifier = Verifier, redirectUri = AndroidRedirect, clientId };

    private static Task<HttpResponseMessage> ExchangeAsync(HttpClient app, object body) =>
        app.PostAsJsonAsync(new Uri(ExchangePath, UriKind.Relative), body, Cancellation);

    private static async Task<string?> SignedInAsAsync(HttpClient app, JsonElement tokens)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("/api/account/me", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ApiJson.Text(tokens, "accessToken"));
        using var response = await app.SendAsync(request, Cancellation);
        var user = (await ApiJson.ReadAsync(response, HttpStatusCode.OK)).GetProperty("user");
        return user.ValueKind == JsonValueKind.Null ? null : ApiJson.Text(user, "username");
    }
}
