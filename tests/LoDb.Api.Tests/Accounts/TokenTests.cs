using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;
using LoDb.Testing;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The tokens of the apps: <c>/token</c> answers a bearer token of five minutes and a
/// refresh token of thirty days, which <c>/refresh</c> exchanges until the account's stamp
/// changes or the account is banned.
/// </summary>
public sealed class TokenTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const int AccessTokenSeconds = 5 * 60;

    private AccountsApp? _app;

    private AccountsApp App =>
        _app ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PasswordGivesABearerAndARefreshToken()
    {
        await App.SeedAsync(new AccountSeed());
        using var app = App.App();

        using var response = await PostAsync(app, "token", Credentials());

        var tokens = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal("Bearer", ApiJson.Text(tokens, "tokenType"));
        Assert.Equal(AccessTokenSeconds, tokens.GetProperty("expiresIn").GetInt32());
        Assert.False(string.IsNullOrEmpty(ApiJson.Text(tokens, "refreshToken")));
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(AccountSeed.Name, await SignedInAsAsync(app, tokens));
    }

    [Fact]
    public async Task AccessTokenLastsFiveMinutes()
    {
        await App.SeedAsync(new AccountSeed());
        using var app = App.App();
        var tokens = await TokensAsync(app);

        App.Clock.Advance(TimeSpan.FromMinutes(6));
        using var signOut = await SendAsync(app, Request(HttpMethod.Post, "logout", tokens));

        Assert.Null(await SignedInAsAsync(app, tokens));
        Assert.Equal(
            "authentication-required",
            await ApiJson.ProblemCodeAsync(signOut, HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task RefreshGivesNewTokensUntilTheStampChanges()
    {
        await App.SeedAsync(new AccountSeed());
        using var app = App.App();
        var tokens = await TokensAsync(app);

        App.Clock.Advance(TimeSpan.FromMinutes(10));
        using var refreshed = await RefreshAsync(app, tokens);
        var renewed = await ApiJson.ReadAsync(refreshed, HttpStatusCode.OK);
        await App.RenewStampAsync(AccountSeed.Name);
        using var revoked = await RefreshAsync(app, renewed);

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
        var tokens = await TokensAsync(app);

        App.Clock.Advance(TimeSpan.FromDays(31));
        using var expired = await RefreshAsync(app, tokens);

        Assert.Equal(
            "invalid-refresh-token",
            await ApiJson.ProblemCodeAsync(expired, HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task BannedAccountGetsNoTokenAndCannotRefresh()
    {
        await App.SeedAsync(new AccountSeed());
        await App.SeedAsync(new AccountSeed
        {
            Username = "Banni_1",
            Email = "banni@example.test",
            Banned = true,
        });
        using var app = App.App();
        var tokens = await TokensAsync(app);

        using var banned = await PostAsync(app, "token", Credentials("Banni_1"));
        await App.BanAsync(AccountSeed.Name);
        using var refresh = await RefreshAsync(app, tokens);

        Assert.Equal(
            "account-banned",
            await ApiJson.ProblemCodeAsync(banned, HttpStatusCode.Forbidden));
        Assert.Equal(
            "invalid-refresh-token",
            await ApiJson.ProblemCodeAsync(refresh, HttpStatusCode.Unauthorized));
    }

    [Theory]
    [InlineData("not-a-token", HttpStatusCode.Unauthorized, "invalid-refresh-token")]
    [InlineData(" ", HttpStatusCode.BadRequest, "validation-failed")]
    public async Task UnreadableRefreshTokenIsRefused(
        string refreshToken,
        HttpStatusCode status,
        string code)
    {
        using var app = App.App();

        using var response = await PostAsync(app, "refresh", new { refreshToken });

        Assert.Equal(code, await ApiJson.ProblemCodeAsync(response, status));
    }

    [Fact]
    public async Task TwoFactorAccountGetsItsTokensWithItsCode()
    {
        await App.SeedAsync(new AccountSeed());
        var secrets = await App.EnableTwoFactorAsync(AccountSeed.Name);
        using var app = App.App();

        using var passwordOnly = await PostAsync(app, "token", Credentials());
        using var withCode = await PostAsync(app, "token", new
        {
            identifier = AccountSeed.Name,
            password = AccountSeed.StrongPassword,
            twoFactorCode = Totp.Code(secrets.Key),
        });

        Assert.Equal(
            "two-factor-required",
            await ApiJson.ProblemCodeAsync(passwordOnly, HttpStatusCode.Unauthorized));
        var tokens = await ApiJson.ReadAsync(withCode, HttpStatusCode.OK);
        using var me = await SendAsync(app, Request(HttpMethod.Get, "me", tokens));
        var user = (await ApiJson.ReadAsync(me, HttpStatusCode.OK)).GetProperty("user");
        Assert.True(user.GetProperty("multiFactor").GetBoolean());
    }

    [Fact]
    public async Task BearerCallsNeedNeitherXsrfTokenNorOriginAndAreAudited()
    {
        var account = await App.SeedAsync(new AccountSeed());
        using var app = App.App();
        var tokens = await TokensAsync(app);
        var request = Request(HttpMethod.Post, "logout", tokens);
        request.Headers.Add(BrowserClient.OriginHeader, "https://evil.example");

        using var signOut = await SendAsync(app, request);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        var trail = await App.AuditAsync();
        Assert.Equal(
            [AuditAction.UserLogin, AuditAction.UserLogout],
            trail.Select(static entry => entry.Action));
        Assert.All(trail, entry => Assert.Equal(account.Id, entry.ActorId));
        Assert.Equal("token", AccountsApp.Meta(trail[0], "channel"));
    }

    public async ValueTask InitializeAsync() => _app = await AccountsApp.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static object Credentials(string identifier = AccountSeed.Name) =>
        new { identifier, password = AccountSeed.StrongPassword };

    private static Task<HttpResponseMessage> PostAsync(HttpClient app, string path, object body) =>
        app.PostAsJsonAsync(Path(path), body, Cancellation);

    private static Uri Path(string path) => new($"/api/account/{path}", UriKind.Relative);

    private static async Task<JsonElement> TokensAsync(HttpClient app)
    {
        using var response = await PostAsync(app, "token", Credentials());
        return await ApiJson.ReadAsync(response, HttpStatusCode.OK);
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient app, JsonElement tokens) =>
        PostAsync(app, "refresh", new { refreshToken = ApiJson.Text(tokens, "refreshToken") });

    // A call as an app makes it: the access token, no cookie, no Origin.
    private static HttpRequestMessage Request(HttpMethod method, string path, JsonElement tokens)
    {
        var request = new HttpRequestMessage(method, Path(path));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ApiJson.Text(tokens, "accessToken"));
        return request;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient app,
        HttpRequestMessage request)
    {
        using (request)
        {
            return await app.SendAsync(request, Cancellation);
        }
    }

    private static async Task<string?> SignedInAsAsync(HttpClient app, JsonElement tokens)
    {
        using var response = await SendAsync(app, Request(HttpMethod.Get, "me", tokens));
        var user = (await ApiJson.ReadAsync(response, HttpStatusCode.OK)).GetProperty("user");
        return user.ValueKind == JsonValueKind.Null ? null : ApiJson.Text(user, "username");
    }
}
