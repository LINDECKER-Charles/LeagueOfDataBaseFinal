using LoDb.Domain.Languages;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
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
    private const int InterfaceLocales = 21;

    [Fact]
    public void DefaultsHoldTheLatestPinnedAndSixteenOthers()
    {
        var options = Resolve(new());

        Assert.Equal(16, options.MaxEntries);
        Assert.Equal(TimeSpan.FromMinutes(1), options.VersionsLifetime);
        Assert.Null(options.PinnedLanguages);
        Assert.Equal(256, options.PinnedBudgetMegabytes);
    }

    [Fact]
    public void DefaultsAdmitAFewColdLoads()
    {
        var options = Resolve(new());

        Assert.Equal(
            (2, 3, 1, 6, TimeSpan.FromSeconds(2), 16),
            (options.ColdLoadConcurrency, options.ColdLoadsPerSecond,
                options.PublicColdLoadsPerSecond, options.ColdLoadBurst,
                options.InternalColdLoadWait, options.MaxPendingLoads));
    }

    [Fact]
    public void DefaultsReuseImageVerdictsTenMinutes()
    {
        var options = Resolve(new());

        Assert.Equal(
            (TimeSpan.FromMinutes(10), 32),
            (options.ImageVerdictsLifetime, options.ImageVerdictVersions));
    }

    [Theory]
    [InlineData("MaxEntries", "0")]
    [InlineData("MaxEntries", "1025")]
    [InlineData("VersionsLifetime", "00:00:00")]
    [InlineData("VersionsLifetime", "01:00:01")]
    [InlineData("PinnedLanguages", "en_US,english")]
    [InlineData("PinnedBudgetMegabytes", "-1")]
    [InlineData("PinnedBudgetMegabytes", "4097")]
    [InlineData("ColdLoadConcurrency", "0")]
    [InlineData("PublicColdLoadsPerSecond", "0")]
    [InlineData("PublicColdLoadsPerSecond", "3")]
    [InlineData("ColdLoadBurst", "2")]
    [InlineData("InternalColdLoadWait", "00:00:00")]
    [InlineData("InternalColdLoadWait", "00:00:31")]
    [InlineData("MaxPendingLoads", "-1")]
    [InlineData("ImageVerdictsLifetime", "00:00:00")]
    [InlineData("ImageVerdictsLifetime", "01:00:01")]
    [InlineData("ImageVerdictVersions", "0")]
    [InlineData("ImageVerdictVersions", "1025")]
    public void OutOfBoundsSettingIsRefused(string setting, string value)
    {
        var failure = Assert.Throws<OptionsValidationException>(
            () => Resolve(new() { [$"{CatalogOptions.SectionName}:{setting}"] = value }));

        Assert.Contains(
            failure.Failures,
            message => message.StartsWith(setting, StringComparison.Ordinal));
    }

    [Fact]
    public void PinnedLanguagesDefaultToEveryInterfaceLocaleEnglishFirst()
    {
        var languages = PinnedLanguageList.Parse(null);

        Assert.NotNull(languages);
        Assert.Equal(InterfaceLocales, languages.Count);
        Assert.Equal(DdragonLanguage.EnUs, languages[0]);
        Assert.Equal(languages.Count, languages.Distinct().Count());
        Assert.Contains(DdragonLanguage.Parse("zh_TW"), languages);
    }

    [Theory]
    [InlineData(" fr_FR, en_US ,fr_FR,", "fr_FR|en_US")]
    [InlineData("ko_KR", "ko_KR")]
    public void PinnedLanguagesKeepTheirOrderOnce(string setting, string expected)
    {
        var languages = PinnedLanguageList.Parse(setting);

        Assert.Equal(expected.Split('|'), languages!.Select(static language => language.Code));
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

    [Fact]
    public void CallerIsPublicUnlessTold()
    {
        var internalDemand = ColdDemand.Queued.For(OnDemandOrigin.Crawler).By(ColdCaller.Internal);

        Assert.Equal(ColdCaller.Public, ColdDemand.Synchronous.Caller);
        Assert.Equal(
            (ColdPolicy.Queued, OnDemandOrigin.Crawler, ColdCaller.Internal),
            (internalDemand.Policy, internalDemand.Origin, internalDemand.Caller));
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
