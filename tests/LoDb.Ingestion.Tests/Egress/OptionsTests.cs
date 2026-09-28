using LoDb.Ingestion.Egress;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Tests.Egress;

public sealed class OptionsTests
{
    [Fact]
    public void UnconfiguredHostsDefaultToDataDragonAndCommunityDragon()
    {
        using var provider = Build([]);

        provider.GetRequiredService<IStartupValidator>().Validate();

        Assert.Equal(
            ["ddragon.leagueoflegends.com", "raw.communitydragon.org"],
            provider.GetRequiredService<IOptions<EgressOptions>>().Value.AllowedHosts);
    }

    // An allow-list that filters down to nothing would leave the service healthy while every
    // fetch fails: the startup refuses it.
    [Theory]
    [InlineData("LoDb:Egress:AllowedHosts", "")]
    [InlineData("LoDb:Egress:AllowedHosts:0", " ")]
    public void EmptyAllowListRefusesToStart(string key, string value)
    {
        using var provider = Build(new() { [key] = value });

        var failure = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains("AllowedHosts", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("FetchConcurrency", "0")]
    [InlineData("AttemptTimeout", "00:00:00")]
    [InlineData("MaxResponseBytes", "0")]
    public void ValueThePipelineWouldRejectRefusesToStart(string key, string value)
    {
        using var provider = Build(new() { ["LoDb:Egress:" + key] = value });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddLoDbEgress(configuration)
            .BuildServiceProvider();
    }
}
