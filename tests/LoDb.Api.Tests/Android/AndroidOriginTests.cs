using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LoDb.Testing;

namespace LoDb.Api.Tests.Android;

/// <summary>
/// The Android app calls the API from <c>https://localhost</c>, the origin its WebView
/// serves the embedded bundle from (ADR 0007): CORS and the forgery guard let that origin,
/// and only that one, sign in and call <c>/api</c> with a bearer token.
/// </summary>
public sealed class AndroidOriginTests(PostgresContainerFixture postgres)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private const string TokenPath = "/api/account/token";
    private const string SessionPath = "/api/account/me";
    private const string PolicyPath = "/api/client-policy";

    private const string AllowOrigin = "Access-Control-Allow-Origin";
    private const string AllowCredentials = "Access-Control-Allow-Credentials";
    private const string AllowHeaders = "Access-Control-Allow-Headers";
    private const string AllowMethods = "Access-Control-Allow-Methods";
    private const string ExposeHeaders = "Access-Control-Expose-Headers";

    // What the app adds to a call: its bearer token, a JSON body, and the client header of
    // the update policy (ADR 0008).
    private const string AppHeaders = "authorization,content-type,x-lodb-client";

    private AndroidApiHost? _host;

    private AndroidApiHost Host =>
        _host ?? throw new InvalidOperationException("The host is not started yet.");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PreflightLetsTheAppSendItsHeaders()
    {
        using var client = Host.Client();

        using var response = await client.SendAsync(
            Preflight(TokenPath, AndroidApiHost.AppOrigin),
            Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AndroidApiHost.AppOrigin, Header(response, AllowOrigin));
        Assert.Contains("POST", Header(response, AllowMethods), StringComparison.Ordinal);
        Assert.Equal(
            AppHeaders.Split(','),
            Header(response, AllowHeaders)!.ToLowerInvariant().Split(','));
        // The app authenticates by bearer token, never by the site's cookies.
        Assert.Null(Header(response, AllowCredentials));
    }

    [Fact]
    public async Task AppSignsInAndCallsTheApiWithItsBearerToken()
    {
        await Host.SeedAccountAsync();
        using var client = Host.Client();

        using var signIn = await client.SendAsync(
            FromApp(HttpMethod.Post, TokenPath, AndroidApiHost.AppOrigin, new
            {
                identifier = AndroidApiHost.Username,
                password = AndroidApiHost.Password,
            }),
            Cancellation);
        var tokens = await ReadAsync(signIn);
        using var session = FromApp(HttpMethod.Get, SessionPath, AndroidApiHost.AppOrigin);
        session.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens.GetProperty("accessToken").GetString());
        using var me = await client.SendAsync(session, Cancellation);
        var user = (await ReadAsync(me)).GetProperty("user");

        Assert.Equal(AndroidApiHost.AppOrigin, Header(signIn, AllowOrigin));
        Assert.Equal(AndroidApiHost.AppOrigin, Header(me, AllowOrigin));
        Assert.Equal(AndroidApiHost.Username, user.GetProperty("username").GetString());
    }

    [Fact]
    public async Task AppReadsTheRetryAfterOfTheApi()
    {
        using var client = Host.Client();

        using var response = await client.SendAsync(
            FromApp(HttpMethod.Get, PolicyPath, AndroidApiHost.AppOrigin),
            Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AndroidApiHost.AppOrigin, Header(response, AllowOrigin));
        Assert.Equal("Retry-After", Header(response, ExposeHeaders));
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("https://localhost:8443")]
    [InlineData("capacitor://localhost")]
    [InlineData("https://localhost.evil.example")]
    public async Task LookalikeOriginsAreRefused(string origin)
    {
        await Host.SeedAccountAsync();
        using var client = Host.Client();

        using var preflight = await client.SendAsync(Preflight(TokenPath, origin), Cancellation);
        using var signIn = await client.SendAsync(
            FromApp(HttpMethod.Post, TokenPath, origin, new
            {
                identifier = AndroidApiHost.Username,
                password = AndroidApiHost.Password,
            }),
            Cancellation);

        Assert.Null(Header(preflight, AllowOrigin));
        Assert.Equal(HttpStatusCode.Forbidden, signIn.StatusCode);
        Assert.Null(Header(signIn, AllowOrigin));
    }

    public async ValueTask InitializeAsync() => _host = await AndroidApiHost.StartAsync(postgres);

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    private static HttpRequestMessage Preflight(string path, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, new Uri(path, UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", AppHeaders);
        return request;
    }

    private static HttpRequestMessage FromApp(
        HttpMethod method,
        string path,
        string origin,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        request.Headers.Add("Origin", origin);
        return request;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
