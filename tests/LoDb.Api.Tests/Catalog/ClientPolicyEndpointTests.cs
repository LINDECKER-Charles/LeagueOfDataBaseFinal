using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.ClientPolicy;
using LoDb.Testing;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The skeleton of the client policy: each app's minimum and latest version, from the
/// configuration, refused at startup when they cannot hold.
/// </summary>
public sealed class ClientPolicyEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PolicyListsEveryPlatformFromTheConfiguration()
    {
        await using var factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?>
            {
                ["LoDb:ClientPolicy:Desktop:MinimumVersion"] = "1.2.0",
                ["LoDb:ClientPolicy:Desktop:LatestVersion"] = "1.4.1",
            },
        };
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/client-policy", UriKind.Relative),
            Token);
        var policy = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token))
            .RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=60", CacheHeaderTests.CacheControlOf(response));
        Assert.Equal(["desktop", "android"], policy.Pluck("platforms", "platform"));
        var desktop = policy.Items("platforms")[0];
        var android = policy.Items("platforms")[1];
        Assert.Equal(("1.2.0", "1.4.1"),
            (desktop.Text("minimumVersion"), desktop.Text("latestVersion")));
        Assert.True(android.IsNull("minimumVersion"));
        Assert.True(android.IsNull("latestVersion"));
    }

    [Theory]
    [InlineData("1.2", null, "Desktop:MinimumVersion must read as 1.2.3")]
    [InlineData(null, "1.2.3-beta", "Desktop:LatestVersion must read as 1.2.3")]
    [InlineData(" 1.2.3", null, "Desktop:MinimumVersion must read as 1.2.3")]
    [InlineData("2.0.0", "1.9.9", "Desktop:MinimumVersion must not exceed LatestVersion")]
    public void InvalidVersionsAreRefused(string? minimum, string? latest, string reason)
    {
        var options = new ClientPolicyOptions
        {
            Desktop = new PlatformVersionOptions
            {
                MinimumVersion = minimum,
                LatestVersion = latest,
            },
        };

        var result = new ClientPolicyOptionsValidator().Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(reason, result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("1.0.0", null)]
    [InlineData("1.9.10", "1.10.0")]
    public void ConsistentVersionsAreAccepted(string? minimum, string? latest)
    {
        var options = new ClientPolicyOptions
        {
            Android = new PlatformVersionOptions
            {
                MinimumVersion = minimum,
                LatestVersion = latest,
            },
        };

        var result = new ClientPolicyOptionsValidator().Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }
}
