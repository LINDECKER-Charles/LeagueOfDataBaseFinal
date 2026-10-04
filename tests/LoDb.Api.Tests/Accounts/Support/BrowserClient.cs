using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>
/// A page of the site in a browser: its cookies kept, its unsafe calls sent as the Angular
/// front sends them, JSON with the page's <c>Origin</c> and the token of the
/// <c>XSRF-TOKEN</c> cookie in <c>X-XSRF-TOKEN</c>.
/// </summary>
public sealed class BrowserClient : IDisposable
{
    public const string SessionCookie = "__Host-lodb.session";
    public const string XsrfCookie = "XSRF-TOKEN";
    public const string XsrfHeader = "X-XSRF-TOKEN";
    public const string OriginHeader = "Origin";
    public const string SessionPath = "/api/account/me";

    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _client;

    private BrowserClient(WebApplicationFactory<Program> host) =>
        _client = host.CreateDefaultClient(AccountsApp.BaseAddress, new CookieJar(_cookies));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>A browser on the site: it keeps its cookies and follows no redirect.</summary>
    public static BrowserClient Open(WebApplicationFactory<Program> host) => new(host);

    /// <summary>The value of a cookie the browser holds for the site, or null.</summary>
    public string? Cookie(string name) =>
        _cookies.GetCookies(AccountsApp.BaseAddress)[name]?.Value;

    public Task<HttpResponseMessage> GetAsync(string path) =>
        _client.GetAsync(new Uri(path, UriKind.Relative), Cancellation);

    /// <summary>A POST as the front builds it, which a test may alter before sending it.</summary>
    public HttpRequestMessage Post(string path, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        request.Headers.Add(OriginHeader, AccountsApp.SiteOrigin);
        if (Cookie(XsrfCookie) is { } token)
        {
            request.Headers.Add(XsrfHeader, token);
        }

        return request;
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) =>
        _client.SendAsync(request, Cancellation);

    public async Task<HttpResponseMessage> PostAsync(string path, object? body = null)
    {
        using var request = Post(path, body);
        return await SendAsync(request);
    }

    /// <summary>Signs in with the login form.</summary>
    public Task<HttpResponseMessage> SignInAsync(
        string identifier,
        string password = AccountSeed.StrongPassword,
        bool rememberMe = false) =>
        PostAsync("/api/account/login", new { identifier, password, rememberMe });

    /// <summary>The account <c>/me</c> answers; null when signed out.</summary>
    public async Task<JsonElement?> SessionUserAsync()
    {
        using var response = await GetAsync(SessionPath);
        var session = await ApiJson.ReadAsync(response, HttpStatusCode.OK);
        var user = session.GetProperty("user");
        return user.ValueKind == JsonValueKind.Null ? null : user;
    }

    /// <summary>The username of the signed-in account; null when signed out.</summary>
    public async Task<string?> SignedInAsAsync() =>
        await SessionUserAsync() is { } user ? ApiJson.Text(user, "username") : null;

    public void Dispose() => _client.Dispose();

    // The cookies of a browser. CookieContainerHandler would reject the correlation cookie
    // of the Google handler, whose path is the callback's, not the one of the page that
    // sets it; a browser keeps it for its own path.
    private sealed class CookieJar(CookieContainer cookies) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var header = cookies.GetCookieHeader(uri);
            if (header.Length > 0)
            {
                request.Headers.Add(HeaderNames.Cookie, header);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
            {
                foreach (var cookie in SetCookieHeaderValue.ParseList([.. values]))
                {
                    var scope = cookie.Path.HasValue ? new Uri(uri, cookie.Path.Value) : uri;
                    cookies.SetCookies(scope, cookie.ToString());
                }
            }

            return response;
        }
    }
}
