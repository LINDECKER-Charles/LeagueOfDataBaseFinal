using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LoDb.Desktop.Tests.Support;

/// <summary>
/// A stand-in for the remote API on a loopback port: the token endpoints of L4.2 and an echo
/// of the headers the proxy forwards. Nothing leaves the machine.
/// </summary>
internal sealed class FakeApi : IAsyncDisposable
{
    public const string Identifier = "player";
    public const string Password = "correct-password";
    public const string GoodGoogleCode = "good-code";
    public const string EchoPath = "/api/echo";
    public const int AccessLifetimeSeconds = 300;

    private static readonly string[] EchoMethods = ["GET", "POST"];

    private WebApplication? _application;
    private int _issued;

    /// <summary>The origin to pass as the host's API origin.</summary>
    public Uri Origin { get; private set; } = null!;

    /// <summary>Every refresh token handed out and not used yet.</summary>
    public ConcurrentDictionary<string, bool> RefreshTokens { get; } = new();

    public ConcurrentQueue<JsonObject> GoogleExchanges { get; } = new();

    public ConcurrentQueue<string> RefreshesReceived { get; } = new();

    /// <summary>When set, every refresh is answered 401 invalid-refresh-token.</summary>
    public bool IsRefreshRefused { get; set; }

    /// <summary>The access token of the last grant.</summary>
    public string LastAccessToken => $"access-{Volatile.Read(ref _issued)}";

    public static async Task<FakeApi> StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0));
        var api = new FakeApi { _application = builder.Build() };
        api.Map(api._application);
        await api._application.StartAsync(cancellationToken);
        var bound = api._application.Services
            .GetRequiredService<IServer>()
            .Features
            .GetRequiredFeature<IServerAddressesFeature>()
            .Addresses
            .Single();
        api.Origin = new Uri(bound);
        return api;
    }

    public async ValueTask DisposeAsync()
    {
        if (_application is not null)
        {
            await _application.StopAsync(CancellationToken.None);
            await _application.DisposeAsync();
        }
    }

    private void Map(WebApplication application)
    {
        application.MapPost("/api/account/token", (JsonObject body) => SignIn(body));
        application.MapPost("/api/account/refresh", (JsonObject body) => Refresh(body));
        application.MapPost(
            "/api/account/google/app/exchange",
            (JsonObject body) => Exchange(body));
        application.MapMethods(EchoPath, EchoMethods, (HttpContext context) => Echo(context));
        application.MapGet("/api/meta", () => Results.Json(new { status = "ok" }));
    }

    private IResult SignIn(JsonObject body) =>
        (string?)body["identifier"] == Identifier && (string?)body["password"] == Password
            ? Grant()
            : Problem(StatusCodes.Status401Unauthorized, "invalid-credentials");

    private IResult Refresh(JsonObject body)
    {
        var refreshToken = (string?)body["refreshToken"] ?? string.Empty;
        RefreshesReceived.Enqueue(refreshToken);
        return !IsRefreshRefused && RefreshTokens.TryRemove(refreshToken, out _)
            ? Grant()
            : Problem(StatusCodes.Status401Unauthorized, "invalid-refresh-token");
    }

    private IResult Exchange(JsonObject body)
    {
        GoogleExchanges.Enqueue(body);
        return (string?)body["code"] == GoodGoogleCode
            ? Grant()
            : Problem(StatusCodes.Status403Forbidden, "account-banned");
    }

    private IResult Grant()
    {
        var number = Interlocked.Increment(ref _issued);
        var refreshToken = $"refresh-{number}";
        RefreshTokens[refreshToken] = true;
        return Results.Json(new
        {
            tokenType = "Bearer",
            accessToken = $"access-{number}",
            expiresIn = AccessLifetimeSeconds,
            refreshToken,
        });
    }

    private static IResult Echo(HttpContext context)
    {
        // A cookie the API would set: the proxy must not let it reach the WebView.
        context.Response.Headers.SetCookie = "api-cookie=1; Path=/";
        var headers = context.Request.Headers;
        return Results.Json(new
        {
            authorization = headers.Authorization.ToString(),
            cookie = headers.Cookie.ToString(),
            origin = headers.Origin.ToString(),
            referer = headers.Referer.ToString(),
            client = headers["X-LoDb-Client"].ToString(),
        });
    }

    private static IResult Problem(int status, string code) =>
        Results.Problem(statusCode: status, extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
        });
}
