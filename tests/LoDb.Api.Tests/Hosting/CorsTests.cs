using System.Net;
using LoDb.Testing;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// <c>/api</c> is closed to every origin but the Android app; <c>/v1</c> answers any origin
/// for reading, with go-api's exact headers.
/// </summary>
/// <remarks>No test changes the host's state: one host serves them all.</remarks>
public sealed class CorsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string AndroidOrigin = "https://localhost";
    private const string AllowOrigin = "Access-Control-Allow-Origin";
    private const string AllowMethods = "Access-Control-Allow-Methods";
    private const string AllowHeaders = "Access-Control-Allow-Headers";
    private const string AllowCredentials = "Access-Control-Allow-Credentials";

    // What the app adds to a call: its bearer token, a JSON body, and the client header of
    // the update policy (ADR 0008).
    private const string AppHeaders = "authorization,content-type,x-lodb-client";

    [Fact]
    public async Task AppApiLetsTheAndroidAppSendItsHeaders()
    {
        using var preflight = Preflight("/api/account/token", AndroidOrigin);
        preflight.Headers.Add("Access-Control-Request-Headers", AppHeaders);

        using var response = await SendAsync(preflight);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AndroidOrigin, Header(response, AllowOrigin));
        Assert.Contains("POST", Header(response, AllowMethods), StringComparison.Ordinal);
        Assert.Equal(
            AppHeaders.Split(','),
            Header(response, AllowHeaders)!.ToLowerInvariant().Split(','));
        // The app authenticates by bearer token, never by the site's cookies.
        Assert.Null(Header(response, AllowCredentials));
    }

    [Fact]
    public async Task AppApiRefusesAnyOtherOrigin()
    {
        using var response = await SendAsync(Preflight("/api/builds", "https://example.com"));

        Assert.Null(Header(response, AllowOrigin));
    }

    // Answered before routing: any path under /v1 gets the same answer.
    [Fact]
    public async Task PublicApiAnswersItsPreflightLikeGoApi()
    {
        using var response = await SendAsync(
            Preflight("/v1/profiles/someone", "https://example.com"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", Header(response, AllowOrigin));
        Assert.Equal("GET, OPTIONS", Header(response, AllowMethods));
        Assert.Equal("Authorization, X-Api-Key", Header(response, AllowHeaders));
    }

    [Fact]
    public async Task PublicApiHeadersAlsoCoverOtherResponses()
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, "/v1/unknown"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("*", Header(response, AllowOrigin));
    }

    [Theory]
    [InlineData("/v1")]
    [InlineData("/V1/profiles/someone")]
    [InlineData("/v10/profiles/someone")]
    public async Task OnlyTheV1PrefixGetsThePublicHeaders(string path)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, path));

        Assert.Null(Header(response, AllowOrigin));
    }

    private static HttpRequestMessage Preflight(string path, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        using (request)
        {
            using var client = factory.CreateClient();
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
