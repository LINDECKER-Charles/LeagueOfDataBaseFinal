using System.Net;
using LoDb.Api.Hosting;
using LoDb.Api.Tests.Accounts.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// The hourly limits by client address, which the contact and donation lots put on their
/// endpoints: a client is counted by its address, or by its IPv6 /64, and refused with a
/// <c>rate-limited</c> problem once its hour is used up.
/// </summary>
public sealed class RateLimitingPoliciesTests
{
    private const string ContactPath = "/contact";
    private const string CheckoutPath = "/checkout";
    private const int ContactsPerHour = 5;
    private const int CheckoutsPerHour = 10;
    private const string Client = "203.0.113.7";
    private const string OtherClient = "203.0.113.8";

    // Stands in for the address the forwarded headers resolve.
    private const string ClientHeader = "X-Test-Client";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:85a3:8d3:1319:8a2e:370:7348", "2001:db8:85a3:8d3::/64")]
    [InlineData("2001:db8:85a3:8d3::1", "2001:db8:85a3:8d3::/64")]
    [InlineData("::1", "::/64")]
    public void ClientIsCountedByItsAddressOrItsIpv6Network(string address, string key) =>
        Assert.Equal(key, RateLimitingPolicies.ClientKey(IPAddress.Parse(address)));

    [Fact]
    public void ClientWithoutAddressSharesTheUnknownCount() =>
        Assert.Equal("unknown", RateLimitingPolicies.ClientKey(null));

    [Theory]
    [InlineData(ContactPath, ContactsPerHour)]
    [InlineData(CheckoutPath, CheckoutsPerHour)]
    public async Task ClientIsRefusedOnceItsHourIsUsedUp(string path, int perHour)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < perHour; attempt++)
        {
            using var allowed = await PostAsync(client, path, Client);
            statuses.Add(allowed.StatusCode);
        }

        using var refused = await PostAsync(client, path, Client);
        using var otherClient = await PostAsync(client, path, OtherClient);

        Assert.All(statuses, static status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(
            "rate-limited",
            await ApiJson.ProblemCodeAsync(refused, HttpStatusCode.TooManyRequests));
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal(HttpStatusCode.OK, otherClient.StatusCode);
    }

    [Fact]
    public async Task EachPolicyCountsOnItsOwn()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        for (var attempt = 0; attempt < ContactsPerHour; attempt++)
        {
            using var contact = await PostAsync(client, ContactPath, Client);
        }

        using var checkout = await PostAsync(client, CheckoutPath, Client);

        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
    }

    // The policies as the lots will use them, behind the host's middleware order.
    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddLoDbRateLimiting(builder.Configuration);
        var app = builder.Build();
        app.Use(static (context, next) =>
        {
            var client = context.Request.Headers[ClientHeader].ToString();
            if (IPAddress.TryParse(client, out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            return next(context);
        });
        app.UseRateLimiter();
        app.MapPost(ContactPath, static () => TypedResults.Ok())
            .RequireRateLimiting(RateLimitingPolicies.Contact);
        app.MapPost(CheckoutPath, static () => TypedResults.Ok())
            .RequireRateLimiting(RateLimitingPolicies.DonationCheckout);
        await app.StartAsync(Cancellation);
        return app;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        string address)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(path, UriKind.Relative));
        request.Headers.Add(ClientHeader, address);
        return await client.SendAsync(request, Cancellation);
    }
}
