using LoDb.Infrastructure.Audience;
using LoDb.Infrastructure.Audience.Umami;
using LoDb.Infrastructure.Tests.Audience.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Infrastructure.Tests.Audience;

/// <summary>
/// The <c>umami</c> client, as the audience zone registers it, reaches the configured host
/// over https alone: anything else is refused before it leaves the process, so the read-only
/// account's credentials never go elsewhere.
/// </summary>
public sealed class UmamiDestinationTests
{
    private const string Host = "stats.example.test";

    // Another host, then the configured host over cleartext http.
    [Theory]
    [InlineData("https://elsewhere.example.test/api/auth/login")]
    [InlineData("http://stats.example.test/api/auth/login")]
    public async Task TheClientRefusesAnyOtherDestination(string url)
    {
        using var umami = new FakeUmami();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AudienceOptions.SectionName}:{nameof(AudienceOptions.Host)}"] = Host,
            })
            .Build();
        var services = new ServiceCollection().AddLoDbAudience(configuration);
        services.AddHttpClient(UmamiClient.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => umami);
        await using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(UmamiClient.ClientName);

        await Assert.ThrowsAsync<UmamiException>(
            () => client.GetAsync(new Uri(url), TestContext.Current.CancellationToken));

        Assert.Empty(umami.Requests);
    }
}
