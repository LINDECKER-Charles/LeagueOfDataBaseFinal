using System.Net;
using LoDb.Api.Modules.Catalog.WarmUp.Limits;
using LoDb.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Catalog.WarmUp;

/// <summary>
/// The bounds of the loader: streams open at once, in all and per client; warm-ups started per
/// client and hour; settings refused at startup when out of bounds. A refusal never breaks
/// the contract: a failed frame, or a 429 the loader treats alike.
/// </summary>
public sealed class WarmUpLimitsTests
{
    private const string WarmUp = "/api/catalog/latest/en_US/warm-up";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void StreamsAreBoundedInAllAndPerClient()
    {
        var streams = new WarmUpStreams(Options.Create(new WarmUpOptions
        {
            MaxStreams = 3,
            MaxStreamsPerClient = 2,
        }));

        var first = streams.TryOpen("a");
        Assert.NotNull(first);
        Assert.NotNull(streams.TryOpen("a"));
        Assert.Null(streams.TryOpen("a"));
        Assert.NotNull(streams.TryOpen("b"));
        Assert.Null(streams.TryOpen("c"));
        first.Dispose();
        first.Dispose();
        Assert.NotNull(streams.TryOpen("c"));
        Assert.Null(streams.TryOpen("d"));
    }

    [Fact]
    public async Task StreamFindingNoPlaceEndsWithAPendingFailure()
    {
        await using var factory = Factory(new()
        {
            ["LoDb:Catalog:WarmUp:MaxStreams"] = "1",
            ["LoDb:Catalog:WarmUp:MaxStreamsPerClient"] = "1",
        });
        using var client = factory.CreateClient();
        using var taken = factory.Services.GetRequiredService<WarmUpStreams>().TryOpen("other");

        using var response = await client.GetAsync(new Uri(WarmUp, UriKind.Relative), Token);
        var frames = SseFrames.Parse(await response.Content.ReadAsStringAsync(Token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var frame = Assert.Single(frames);
        Assert.Equal(("failed", "catalog-pending"), (frame.Text("stage"), frame.Text("failure")));
    }

    [Fact]
    public async Task ClientStartingTooManyWarmUpsIsTurnedAway()
    {
        await using var factory = Factory(new() { ["LoDb:Catalog:WarmUp:PerClientPerHour"] = "1" });
        using var client = factory.CreateClient();

        using var first = await client.GetAsync(new Uri(WarmUp, UriKind.Relative), Token);
        using var second = await client.GetAsync(new Uri(WarmUp, UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.NotNull(second.Headers.RetryAfter);
    }

    [Theory]
    [InlineData("MaxStreams", "0")]
    [InlineData("MaxStreamsPerClient", "9")]
    [InlineData("StreamLifetime", "00:00:00")]
    [InlineData("StreamLifetime", "01:00:01")]
    [InlineData("PerClientPerHour", "0")]
    public void OutOfBoundsSettingIsRefused(string setting, string value)
    {
        var failure = new WarmUpOptionsValidator().Validate(null, Bind(setting, value));

        Assert.True(failure.Failed);
        Assert.Contains(
            failure.Failures!,
            message => message.StartsWith(setting, StringComparison.Ordinal));
    }

    [Fact]
    public void DefaultsAreTheServedBounds()
    {
        var options = new WarmUpOptions();

        Assert.True(new WarmUpOptionsValidator().Validate(null, options).Succeeded);
        Assert.Equal(
            (8, 2, TimeSpan.FromMinutes(2), 20),
            (options.MaxStreams, options.MaxStreamsPerClient, options.StreamLifetime,
                options.PerClientPerHour));
    }

    private static ApiFactory Factory(Dictionary<string, string?> settings) =>
        new() { Settings = settings };

    private static WarmUpOptions Bind(string setting, string value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [setting] = value })
            .Build()
            .Get<WarmUpOptions>()
        ?? new WarmUpOptions();
}
