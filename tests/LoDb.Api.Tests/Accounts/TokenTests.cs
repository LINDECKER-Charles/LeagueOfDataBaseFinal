using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Infrastructure.Audit;

namespace LoDb.Api.Tests.Accounts;

/// <summary>
/// The tokens of the apps: <c>/token</c> answers a bearer token of five minutes and a
/// refresh token, which <c>/refresh</c> refuses once the account is banned. How long the
/// refresh token lasts is <see cref="OwnHost.RefreshTokenExpiryTests"/>'.
/// </summary>
/// <remarks>The tests share a host, each with accounts of its own.</remarks>
public sealed class TokenTests(SharedAccountsApp host) : IClassFixture<SharedAccountsApp>
{
    private const int AccessTokenSeconds = 5 * 60;

    private readonly AccountSeed _account = host.Unique();
    private readonly AccountSeed _banned = host.Unique(new AccountSeed { Banned = true });

    private AccountsApp App { get; } = host.App;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PasswordGivesABearerAndARefreshToken()
    {
        await App.SeedAsync(_account);
        using var app = App.App();

        using var response = await PostAsync(app, "token", Credentials(_account.Username));

        var tokens = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        Assert.Equal("Bearer", ApiJson.Text(tokens, "tokenType"));
        Assert.Equal(AccessTokenSeconds, tokens.GetProperty("expiresIn").GetInt32());
        Assert.False(string.IsNullOrEmpty(ApiJson.Text(tokens, "refreshToken")));
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(_account.Username, await SignedInAsAsync(app, tokens));
    }

    [Fact]
    public async Task BannedAccountGetsNoTokenAndCannotRefresh()
    {
        await App.SeedAsync(_account);
        await App.SeedAsync(_banned);
        using var app = App.App();
        var tokens = await AppTokens.SignInAsync(app, _account.Username);

        using var banned = await PostAsync(app, "token", Credentials(_banned.Username));
        await App.BanAsync(_account.Username);
        using var refresh = await PostAsync(
            app,
            "refresh",
            new { refreshToken = ApiJson.Text(tokens, "refreshToken") });

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
        await App.SeedAsync(_account);
        var secrets = await App.EnableTwoFactorAsync(_account.Username);
        using var app = App.App();

        using var passwordOnly = await PostAsync(app, "token", Credentials(_account.Username));
        using var withCode = await PostAsync(app, "token", new
        {
            identifier = _account.Username,
            password = AccountSeed.StrongPassword,
            twoFactorCode = Totp.Code(secrets.Key, App.Clock),
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
        var account = await App.SeedAsync(_account);
        using var app = App.App();
        var tokens = await AppTokens.SignInAsync(app, _account.Username);
        var request = Request(HttpMethod.Post, "logout", tokens);
        request.Headers.Add(BrowserClient.OriginHeader, "https://evil.example");

        using var signOut = await SendAsync(app, request);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        var trail = await App.AuditOfAsync(_account.Username);
        Assert.Equal(
            [AuditAction.UserLogin, AuditAction.UserLogout],
            trail.Select(static entry => entry.Action));
        Assert.All(trail, entry => Assert.Equal(account.Id, entry.ActorId));
        Assert.Equal("token", AccountsApp.Meta(trail[0], "channel"));
    }

    private static object Credentials(string identifier) =>
        new { identifier, password = AccountSeed.StrongPassword };

    private static Task<HttpResponseMessage> PostAsync(HttpClient app, string path, object body) =>
        app.PostAsJsonAsync(Path(path), body, Cancellation);

    private static Uri Path(string path) => new($"/api/account/{path}", UriKind.Relative);

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
