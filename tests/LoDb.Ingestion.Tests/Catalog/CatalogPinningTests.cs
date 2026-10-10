using System.Globalization;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Tests.Pipeline;
using LoDb.Testing;
using LoDb.Testing.Fixtures;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The catalogs of the latest version pinned in memory: the configured languages within the
/// budget, nothing before a promotion, and the pin following the next promotion.
/// </summary>
public sealed class CatalogPinningTests(PostgresContainerFixture postgres)
{
    private const string PinnedEvent = "catalog.pinning.completed";

    private static readonly PatchVersion Latest = DdragonFixtures.Latest;
    private static readonly PatchVersion Previous = DdragonFixtures.Previous;
    private static readonly DdragonLanguage[] Pinned =
        [DdragonLanguage.EnUs, CatalogFixtures.French, DdragonLanguage.Parse("ko_KR")];

    private static CancellationToken Token => IngestionHarness.Token;

    [Fact]
    public async Task LatestCatalogsArePinnedOnceInTheirLanguages()
    {
        await using var harness = await CreateAsync(budgetMegabytes: 400);
        var instance = harness.StartInstance();
        await VersionIngestionTests.IngestAsync(instance, Latest);
        var pinning = instance.Get<ICatalogPinning>();

        await pinning.PinLatestAsync(Token);
        await pinning.PinLatestAsync(Token);

        Assert.True(instance.Get<CatalogCache>().PinnedKeys.ToHashSet()
            .SetEquals(Pinned.Select(static language => new CatalogKey(Latest, language))));
        Assert.Single(instance.Logs, static log => log.Id.Name == PinnedEvent);
    }

    [Fact]
    public async Task LanguagesOverTheBudgetStayEvictable()
    {
        await using var harness = await CreateAsync(budgetMegabytes: 0);
        var instance = harness.StartInstance();
        await VersionIngestionTests.IngestAsync(instance, Latest);

        await instance.Get<ICatalogPinning>().PinLatestAsync(Token);

        Assert.Empty(instance.Get<CatalogCache>().PinnedKeys);
        var pinned = Assert.Single(instance.Logs, static log => log.Id.Name == PinnedEvent);
        Assert.Contains("3 languages left out", pinned.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingIsPinnedBeforeTheFirstPromotion()
    {
        await using var harness = await CreateAsync(budgetMegabytes: 400);
        var instance = harness.StartInstance();

        await instance.Get<ICatalogPinning>().PinLatestAsync(Token);

        Assert.Empty(instance.Get<CatalogCache>().PinnedKeys);
        Assert.DoesNotContain(instance.Logs, static log => log.Id.Name == PinnedEvent);
    }

    [Fact]
    public async Task PinFollowsThePromotion()
    {
        await using var harness = await CreateAsync(budgetMegabytes: 400);
        var instance = harness.StartInstance();
        var pinning = instance.Get<ICatalogPinning>();
        var cache = instance.Get<CatalogCache>();
        await VersionIngestionTests.IngestAsync(instance, Previous);
        await pinning.PinLatestAsync(Token);

        await VersionIngestionTests.IngestAsync(instance, Latest);
        await pinning.PinLatestAsync(Token);

        Assert.All(cache.PinnedKeys, static key => Assert.Equal(Latest, key.Version));
        Assert.Equal(Pinned.Length, cache.PinnedKeys.Count);
        Assert.Equal(Pinned.Length, cache.Keys.Count(static key => key.Version == Previous));
    }

    private Task<IngestionHarness> CreateAsync(int budgetMegabytes) =>
        IngestionHarness.CreateAsync(
            postgres,
            new Dictionary<string, string?>
            {
                ["LoDb:Catalog:PinnedLanguages"] =
                    string.Join(',', Pinned.Select(static language => language.Code)),
                ["LoDb:Catalog:PinnedBudgetMegabytes"] =
                    budgetMegabytes.ToString(CultureInfo.InvariantCulture),
                // The promotion must show at the next pass.
                ["LoDb:Catalog:VersionsLifetime"] = "00:00:00.001",
            });
}
