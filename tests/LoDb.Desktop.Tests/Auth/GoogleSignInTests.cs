using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LoDb.Desktop.Auth.Google;
using LoDb.Desktop.Tests.Support;
using Microsoft.AspNetCore.WebUtilities;

namespace LoDb.Desktop.Tests.Auth;

public sealed class GoogleSignInTests : IAsyncLifetime
{
    private FakeApi _api = null!;
    private DesktopTestHost _host = null!;

    public async ValueTask InitializeAsync()
    {
        _api = await FakeApi.StartAsync(TestContext.Current.CancellationToken);
        _host = await DesktopTestHost.StartAsync(
            new DesktopHostSetup
            {
                ApiOrigin = _api.Origin,
                GoogleClientId = DesktopTestHost.GoogleClientId,
            },
            TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task OpensGoogleInTheSystemBrowserWithPKCEAndALoopbackRedirect()
    {
        using var response = await BeginAsync(_host);
        var authorization = Assert.Single(_host.Setup.Browser.Opened);
        var query = QueryHelpers.ParseQuery(authorization.Query);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("/desktop/auth/session", response.Headers.Location?.OriginalString);
        Assert.Equal(GoogleAuthorization.Endpoint, authorization.GetLeftPart(UriPartial.Path));
        Assert.Equal(DesktopTestHost.GoogleClientId, query["client_id"]);
        Assert.Equal(CallbackUri(), query["redirect_uri"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal(43, query["code_challenge"].ToString().Length);
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.Equal("pending", (string?)(await _host.ReadSessionAsync())["google"]);
    }

    [Fact]
    public async Task SignsInOnTheRedirectWithTheVerifierOfTheChallenge()
    {
        var (state, challenge) = await BeginSignInAsync();

        using var response = await CallbackAsync($"code={FakeApi.GoodGoogleCode}&state={state}");
        var exchange = Assert.Single(_api.GoogleExchanges);
        var session = await _host.ReadSessionAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FakeApi.GoodGoogleCode, (string?)exchange["code"]);
        Assert.Equal(CallbackUri(), (string?)exchange["redirectUri"]);
        Assert.Equal(DesktopTestHost.GoogleClientId, (string?)exchange["clientId"]);
        Assert.Equal(challenge, GoogleAuthorization.ChallengeOf((string)exchange["codeVerifier"]!));
        Assert.True((bool?)session["signedIn"]);
        Assert.True((bool?)session["remembered"]);
        Assert.Equal("idle", (string?)session["google"]);
    }

    [Fact]
    public async Task RefusesARedirectOfAnotherStateAndKeepsTheFlowRunning()
    {
        await BeginSignInAsync();

        using var response = await CallbackAsync($"code={FakeApi.GoodGoogleCode}&state=forged");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_api.GoogleExchanges);
        Assert.Equal("pending", (string?)(await _host.ReadSessionAsync())["google"]);
    }

    [Fact]
    public async Task AcceptsAStateOnce()
    {
        var (state, _) = await BeginSignInAsync();
        using var first = await CallbackAsync($"code={FakeApi.GoodGoogleCode}&state={state}");

        using var replay = await CallbackAsync($"code={FakeApi.GoodGoogleCode}&state={state}");

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Single(_api.GoogleExchanges);
    }

    [Fact]
    public async Task ReportsAConsentTheUserDeclined()
    {
        var (state, _) = await BeginSignInAsync();

        using var response = await CallbackAsync($"error=access_denied&state={state}");
        var session = await _host.ReadSessionAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_api.GoogleExchanges);
        Assert.Equal("failed", (string?)session["google"]);
        Assert.Equal(GoogleFailures.Cancelled, (string?)session["googleFailure"]);
        Assert.False((bool?)session["signedIn"]);
    }

    [Fact]
    public async Task ReportsTheCodeOfARefusedExchange()
    {
        var (state, _) = await BeginSignInAsync();

        using var response = await CallbackAsync($"code=revoked&state={state}");
        var session = await _host.ReadSessionAsync();

        Assert.Equal("failed", (string?)session["google"]);
        Assert.Equal("account-banned", (string?)session["googleFailure"]);
    }

    [Fact]
    public async Task ExpiresAFlowLeftUnanswered()
    {
        var (state, _) = await BeginSignInAsync();
        _host.Setup.Time.Advance(GoogleFlows.Lifetime);

        var session = await _host.ReadSessionAsync();
        using var late = await CallbackAsync($"code={FakeApi.GoodGoogleCode}&state={state}");

        Assert.Equal(GoogleFailures.Expired, (string?)session["googleFailure"]);
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        Assert.Empty(_api.GoogleExchanges);
    }

    [Fact]
    public async Task IsUnavailableInABuildWithoutAGoogleClient()
    {
        await using var host = await DesktopTestHost.StartAsync(
            new DesktopHostSetup { ApiOrigin = _api.Origin },
            TestContext.Current.CancellationToken);

        using var response = await BeginAsync(host);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(GoogleFailures.Unavailable, (string?)problem!["code"]);
        Assert.Empty(host.Setup.Browser.Opened);
    }

    [Fact]
    public async Task ReportsABrowserThatDoesNotStart()
    {
        _host.Setup.Browser.IsAvailable = false;

        using var response = await BeginAsync(_host);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(
            GoogleFailures.BrowserUnavailable,
            (string?)(await _host.ReadSessionAsync())["googleFailure"]);
    }

    private string CallbackUri() =>
        new Uri(_host.Host.Address, "desktop/auth/google/callback").AbsoluteUri;

    private static Task<HttpResponseMessage> BeginAsync(DesktopTestHost host) =>
        host.Client.GetAsync(
            new Uri("/desktop/auth/google?rememberMe=true", UriKind.Relative),
            TestContext.Current.CancellationToken);

    private async Task<(string State, string Challenge)> BeginSignInAsync()
    {
        using var response = await BeginAsync(_host);
        var query = QueryHelpers.ParseQuery(Assert.Single(_host.Setup.Browser.Opened).Query);
        return (query["state"].ToString(), query["code_challenge"].ToString());
    }

    private Task<HttpResponseMessage> CallbackAsync(string query) =>
        _host.Client.GetAsync(
            new Uri($"/desktop/auth/google/callback?{query}", UriKind.Relative),
            TestContext.Current.CancellationToken);
}
