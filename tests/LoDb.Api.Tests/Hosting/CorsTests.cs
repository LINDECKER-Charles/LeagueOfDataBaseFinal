using System.Net;
using LoDb.Testing;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// <c>/api</c> is closed to every origin but the Android app; <c>/v1</c> answers any origin
/// for reading, with go-api's exact headers.
/// </summary>
public sealed class CorsTests
{
    private const string AndroidOrigin = "https://localhost";
    private const string AllowOrigin = "Access-Control-Allow-Origin";
    private const string AllowMethods = "Access-Control-Allow-Methods";
    private const string AllowHeaders = "Access-Control-Allow-Headers";

    [Fact]
    public async Task AppApiAcceptsThePreflightOfTheAndroidApp()
    {
        using var response = await SendAsync(Preflight("/api/builds", AndroidOrigin));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AndroidOrigin, Header(response, AllowOrigin));
    }

    [Fact]
    public async Task AppApiRefusesAnyOtherOrigin()
    {
        using var response = await SendAsync(Preflight("/api/builds", "https://example.com"));

        Assert.Null(Header(response, AllowOrigin));
    }

    [Theory]
    [InlineData("/v1/profiles/someone")]
    [InlineData("/v1/unknown/route")]
    public async Task PublicApiAnswersItsPreflightLikeGoApi(string path)
    {
        using var response = await SendAsync(Preflight(path, "https://example.com"));

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

    private static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        using (request)
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
