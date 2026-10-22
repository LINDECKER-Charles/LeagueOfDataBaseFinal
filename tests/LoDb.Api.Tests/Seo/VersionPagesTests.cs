using LoDb.Api.Modules.Seo;
using LoDb.Api.Modules.Seo.Pages;
using LoDb.Api.Modules.Seo.Sitemaps;
using LoDb.Domain.Languages;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Pipeline.Versions;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The pages of one (version, locale): the four lists, the patch page and every entity they
/// show, in that order (the cursor of a unit of the pass is an index in it), the entities being
/// those the sitemaps list.
/// </summary>
public sealed class VersionPagesTests(PostgresContainerFixture postgres) : IAsyncLifetime
{
    private const string Prefix = "https://league-of-data-base.com/fr/";
    private const string ClassicTwinItem = "items/771004";

    private TestDatabase? _database;
    private ApiFactory? _factory;
    private CatalogSnapshot? _catalog;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private CatalogSnapshot Catalog =>
        _catalog ?? throw new InvalidOperationException("The test is not initialized.");

    // The catalog of the recording, ingested and read as the API reads it.
    public async ValueTask InitializeAsync()
    {
        _database = await postgres.CreateMigratedDatabaseAsync(Token);
        _factory = new ApiFactory
        {
            PostgresConnectionString = _database.AppConnectionString,
            // The production backoff starts at one second: only the attempts matter.
            Settings = new Dictionary<string, string?>
            {
                ["LoDb:Egress:RetryBaseDelay"] = "00:00:00.001",
            },
        };
        var replay = DdragonFixtures.CreateReplay();
        var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddDdragonFixtureReplay(replay)));
        var ingested = await host.Services.GetRequiredService<IVersionIngestion>()
            .IngestAsync(DdragonFixtures.Latest, IngestionRequest.Complete, Token);
        Assert.Equal(VersionIngestionOutcome.Completed, ingested.Outcome);
        var load = await host.Services.GetRequiredService<ICatalogReader>().GetAsync(
            DdragonFixtures.Latest,
            DdragonLanguage.EnUs,
            ColdDemand.Synchronous.By(ColdCaller.Internal),
            Token);
        _catalog = load.Catalog;
        Assert.NotNull(_catalog);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Fact]
    public void PagesAreTheFourListsThePatchPageThenEveryListedEntityOnce()
    {
        var catalog = Catalog;

        var pages = VersionPages.Of(catalog).ToList();

        Assert.Equal(
            [.. SitePages.Lists, VersionPages.PatchChanges],
            pages.Take(SitePages.Lists.Count + 1));
        Assert.Equal(
            [.. VersionPages.ListedEntityPaths(catalog).Select(static path => path.Value)],
            pages.Skip(SitePages.Lists.Count + 1));
        Assert.Equal(pages.Count, pages.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            SitePages.Lists.Count + 1 + ListedEntityCount(catalog),
            pages.Count);
    }

    [Fact]
    public void ClassicTwinsAreListedAndUnlistedEntriesAreNot()
    {
        var catalog = Catalog;

        var pages = VersionPages.Of(catalog).ToHashSet(StringComparer.Ordinal);

        Assert.Contains(
            pages,
            static page => page.StartsWith(ClassicTwinItem, StringComparison.Ordinal));
        Assert.All(
            catalog.Items.Entries.Where(item => !catalog.IsListed(item)),
            item => Assert.DoesNotContain(catalog.PathOf(item).Value, pages));
        Assert.All(
            catalog.Summoners.Entries.Where(spell => !catalog.IsListed(spell)),
            spell => Assert.DoesNotContain(catalog.PathOf(spell).Value, pages));
    }

    [Fact]
    public void HomeAndPatchHistoryAreNotPerVersionPages()
    {
        var catalog = Catalog;

        var pages = VersionPages.Of(catalog).ToList();

        Assert.DoesNotContain(SitePages.Home, pages);
        Assert.DoesNotContain(SitePages.PatchHistory, pages);
    }

    [Fact]
    public void TheSitemapsKeepListingTheSamePatchBoundPages()
    {
        var catalog = Catalog;

        var patchBound = SitemapLocations.PatchBound(Prefix, catalog).ToList();

        Assert.Equal(
            [.. SitePages.PatchBound.Concat(VersionPages.ListedEntityPaths(catalog)
                .Select(static path => path.Value))
                .Select(static path => Prefix + path)],
            patchBound);
    }

    private static int ListedEntityCount(CatalogSnapshot catalog) =>
        catalog.Champions.Entries.Count
        + catalog.ListedItems.Count
        + catalog.Runes.Entries.Count
        + catalog.ListedSummoners.Count;
}
