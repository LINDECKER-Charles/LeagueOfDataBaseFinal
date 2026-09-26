using System.Net;
using System.Net.Http.Json;
using LoDb.Api.Tests.Accounts.Support;
using LoDb.Api.Tests.Analytics.Support;
using LoDb.Testing;

namespace LoDb.Api.Tests.Analytics;

/// <summary>
/// The beacon's limit: one address reports 120 views a minute, and the others keep theirs.
/// </summary>
/// <remarks>
/// A host of its own, without a database: the beacon only enqueues, and the views it takes
/// must not reach the capture tests' queue.
/// </remarks>
public sealed class BeaconRateLimitTests : IAsyncDisposable
{
    private const string ForwardedFor = "X-Forwarded-For";
    private const int ViewsPerMinute = 120;

    private readonly ApiFactory _factory = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OneAddressReportsOneHundredTwentyViewsAMinute()
    {
        using var client = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var view = 0; view < ViewsPerMinute; view++)
        {
            using var response = await SendFromAsync(client, "203.0.113.7");
            statuses.Add(response.StatusCode);
        }

        using var limited = await SendFromAsync(client, "203.0.113.7");
        using var other = await SendFromAsync(client, "203.0.113.8");

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.Accepted, status));
        Assert.Equal(
            "rate-limited",
            await ApiJson.ProblemCodeAsync(limited, HttpStatusCode.TooManyRequests));
        Assert.Equal(HttpStatusCode.Accepted, other.StatusCode);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static async Task<HttpResponseMessage> SendFromAsync(
        HttpClient client,
        string address)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(AnalyticsApiFixture.BeaconPath, UriKind.Relative))
        {
            Content = JsonContent.Create(new { path = "/fr/items" }),
        };
        request.Headers.Add(ForwardedFor, address);
        return await client.SendAsync(request, Cancellation);
    }
}
