using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// <c>POST /api/account/google/app/exchange</c>: an app hands over the code of its Google
/// sign-in with its PKCE verifier, redeemed with the client it names, and gets the tokens of
/// the account, under the same linking rules as the web.
/// </summary>
/// <remarks>
/// The tests share a host: each signs in with a Google account, an e-mail and an account of
/// its own, and Google starts each test anew.
/// </remarks>
public sealed class GoogleAppExchangeTests : IClassFixture<SharedAccountsApp>
{
    private const string ExchangePath = "/api/account/google/app/exchange";
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string AndroidRedirect = "com.leagueofdatabase.app:/oauth2redirect";

    private readonly AccountSeed _account;
    private readonly string _subject;
    private readonly string _gmail;

    public GoogleAppExchangeTests(SharedAccountsApp host)
    {
        ArgumentNullException.ThrowIfNull(host);
        App = host.App;
        App.GoogleServer.Reset();
        _account = host.Unique();
        var number = host.NextNumber();
        _subject = string.Create(CultureInfo.InvariantCulture, $"1102484959{number:D11}");
        _gmail = string.Create(CultureInfo.InvariantCulture, $"joueur{number}@gmail.test");
    }

    private AccountsApp App { get; }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AndroidCodeGivesTheTokensOfTheAccount()
    {
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = _subject,
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
        var login = Assert.Single(await App.AuditOfAsync("Joueur"));
        Assert.Equal(
            (AuditAction.UserLogin, "google", "token"),
            (login.Action, AccountsApp.Meta(login, "method"), AccountsApp.Meta(login, "channel")));
    }

    [Theory]
    [InlineData(AccountsApp.DesktopClientId, AccountsApp.DesktopClientSecret)]
    [InlineData(null, AccountsApp.WebClientSecret)]
    public async Task CodeIsRedeemedWithTheSecretOfItsClient(string? clientId, string secret)
    {
        App.GoogleServer.Account = NewAccount();
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

    [Theory]
    [InlineData("code with spaces", Verifier, "code")]
    [InlineData(FakeGoogle.Code, "too-short-for-pkce", "codeVerifier")]
    public async Task MalformedCodeOrVerifierNeverReachesGoogle(
        string code,
        string verifier,
        string field)
    {
        App.GoogleServer.Account = NewAccount();
        using var app = App.App();

        using var response = await ExchangeAsync(app, new
        {
            code,
            codeVerifier = verifier,
            redirectUri = AndroidRedirect,
            clientId = AccountsApp.AndroidClientId,
        });

        var errors = await ApiJson.FieldErrorsAsync(response);
        Assert.Equal(["malformed"], Assert.Single(errors).Value);
        Assert.Equal(field, Assert.Single(errors).Key);
        Assert.Empty(App.GoogleServer.Exchanges);
    }

    [Fact]
    public async Task CodeGoogleRefusesGivesNoToken()
    {
        App.GoogleServer.Account = NewAccount();
        using var app = App.App();

        using var response = await ExchangeAsync(app, Exchange(code: "stolen"));

        Assert.Equal(
            "google-exchange-failed",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.BadRequest));
        Assert.Equal(
            ["0"],
            await App.QueryAsync($"SELECT count(*) FROM users WHERE email = '{_gmail}'"));
    }

    [Fact]
    public async Task UnverifiedEmailNeverTakesTheAccountThatUsesIt()
    {
        await App.SeedAsync(_account);
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = _subject,
            Email = _account.Email,
            EmailVerified = false,
        };
        using var app = App.App();

        using var response = await ExchangeAsync(app, Exchange());

        Assert.Equal(
            "google-email-unverified",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Conflict));
        Assert.Null((await App.FindAsync(_account.Username)).GoogleId);
    }

    [Fact]
    public async Task BannedAccountGetsNoToken()
    {
        await App.SeedAsync(_account with { GoogleId = _subject, Banned = true });
        App.GoogleServer.Account = new GoogleAccount
        {
            Subject = _subject,
            Email = _account.Email,
        };
        using var app = App.App();

        using var response = await ExchangeAsync(app, Exchange());

        Assert.Equal(
            "account-banned",
            await ApiJson.ProblemCodeAsync(response, HttpStatusCode.Forbidden));
        var refusal = Assert.Single(await App.AuditOfAsync(_account.Username, _account.Email));
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

    // A Google account of this test only, its e-mail used by no account yet.
    private GoogleAccount NewAccount() => new() { Subject = _subject, Email = _gmail };
}
