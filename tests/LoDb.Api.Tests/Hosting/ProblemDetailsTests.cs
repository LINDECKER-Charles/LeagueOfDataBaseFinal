using System.Net;
using LoDb.Api.Tests.Health;
using LoDb.Testing;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// Under <c>/api</c> an error without a body becomes a ProblemDetails; the other surfaces keep
/// their own format.
/// </summary>
/// <remarks>No test changes the host's state: one host serves them all.</remarks>
public sealed class ProblemDetailsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task UnknownAppRouteAnswersAProblemDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/unknown", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await HealthEndpointTests.ReadBodyAsync(response);
        Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task UnknownRouteOutsideTheAppApiKeepsAnEmptyBody()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/unknown", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Empty(body);
    }
}
