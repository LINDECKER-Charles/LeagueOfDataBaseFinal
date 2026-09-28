using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Queue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The settings of the catalog zone, refused at startup when out of bounds, and the demand
/// a cold read is made with.
/// </summary>
public sealed class CatalogOptionsTests
{
    [Fact]
    public void DefaultsHoldSixteenCatalogsAndTheVersionsAMinute()
    {
        var options = Resolve(new());

        Assert.Equal(16, options.MaxEntries);
        Assert.Equal(TimeSpan.FromMinutes(1), options.VersionsLifetime);
    }

    [Theory]
    [InlineData("MaxEntries", "0")]
    [InlineData("MaxEntries", "1025")]
    [InlineData("VersionsLifetime", "00:00:00")]
    [InlineData("VersionsLifetime", "01:00:01")]
    public void OutOfBoundsSettingIsRefused(string setting, string value)
    {
        var failure = Assert.Throws<OptionsValidationException>(
            () => Resolve(new() { [$"{CatalogOptions.SectionName}:{setting}"] = value }));

        Assert.Contains(
            failure.Failures,
            message => message.StartsWith(setting, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ColdPolicy.Synchronous, OnDemandOrigin.Visitor, ColdPolicy.Synchronous)]
    [InlineData(ColdPolicy.Synchronous, OnDemandOrigin.Crawler, ColdPolicy.Queued)]
    [InlineData(ColdPolicy.Queued, OnDemandOrigin.Crawler, ColdPolicy.Queued)]
    [InlineData(ColdPolicy.StoredOnly, OnDemandOrigin.Crawler, ColdPolicy.StoredOnly)]
    public void CrawlerNeverWaitsForAnIngestion(
        ColdPolicy policy,
        OnDemandOrigin origin,
        ColdPolicy effective)
    {
        var demand = new ColdDemand(policy).For(origin);

        Assert.Equal((policy, origin, effective), (demand.Policy, demand.Origin, demand.Effective));
    }

    private static CatalogOptions Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ServiceCollection()
            .AddLoDbCatalog(configuration)
            .BuildServiceProvider();
        return provider.GetRequiredService<IOptions<CatalogOptions>>().Value;
    }
}
