using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Canonicals;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The catalogs the canonical pages of a version are decided from: the latest version's and
/// the next listed version's, en_US, the next one for a single page only when the latest
/// version lacks its entity, none for the latest version itself, and no decision while one
/// of them is pending. Molten Edge (3371) is in 7.22.1 and 8.7.1, gone from 16.18.1 on.
/// </summary>
public sealed class CanonicalReaderTests
{
    private static readonly PatchVersion Latest = CatalogFixtures.Latest;
    private static readonly PatchVersion Previous = PatchVersion.Parse("16.18.1");
    private static readonly PatchVersion Archived = PatchVersion.Parse("8.7.1");
    private static readonly PatchVersion Older = PatchVersion.Parse("7.22.1");
    private static readonly PatchVersion[] Listed = [Latest, Previous, Archived, Older];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReadsTheLatestAndTheNextListedVersionsInEnglish()
    {
        var reader = await ReaderAsync(Latest, Archived);

        var references = await new CanonicalReader(reader).ReadAsync(Older, Demand, Token);

        Assert.Equal([Latest, Archived], reader.Reads);
        Assert.All(
            reader.Languages,
            static language => Assert.Equal(DdragonLanguage.EnUs, language));
        Assert.Equal(Archived, references?.NextCatalog?.Version);
        Assert.Equal(Latest, references?.LatestCatalog?.Version);
    }

    [Fact]
    public async Task VersionJustBeforeTheLatestReadsTheLatestAlone()
    {
        var reader = await ReaderAsync(Latest);

        var references = await new CanonicalReader(reader).ReadAsync(Previous, Demand, Token);

        Assert.Equal([Latest], reader.Reads);
        Assert.Same(references?.LatestCatalog, references?.NextCatalog);
    }

    [Fact]
    public async Task PageOfAnEntityTheLatestVersionHoldsReadsTheLatestAlone()
    {
        var reader = await ReaderAsync(Latest);

        var location = await new CanonicalReader(reader)
            .LocateAsync(Older, await PathAsync(Older, "1001"), Demand, Token);

        Assert.Equal([Latest], reader.Reads);
        Assert.Equal("items/1001-boots", location?.Canonical?.Path.Value);
    }

    [Fact]
    public async Task PageOfADroppedEntityReadsTheNextListedVersion()
    {
        var reader = await ReaderAsync(Latest, Archived);

        var location = await new CanonicalReader(reader)
            .LocateAsync(Older, await PathAsync(Older, "3371"), Demand, Token);

        Assert.Equal([Latest, Archived], reader.Reads);
        Assert.NotNull(location);
        Assert.Null(location.Canonical);
    }

    [Fact]
    public async Task LatestVersionReadsNoCatalog()
    {
        var reader = await ReaderAsync();

        var references = await new CanonicalReader(reader).ReadAsync(Latest, Demand, Token);

        Assert.Empty(reader.Reads);
        Assert.Equal(Latest, references?.Latest);
    }

    [Fact]
    public async Task PendingCatalogLeavesThePagesUndecided()
    {
        var reader = await ReaderAsync(Latest);

        var references = await new CanonicalReader(reader).ReadAsync(Older, Demand, Token);

        Assert.Null(references);
    }

    private static ColdDemand Demand => ColdDemand.Synchronous;

    private static async Task<CanonicalPath> PathAsync(PatchVersion version, string item)
    {
        var catalog = await CatalogFixtures.ReadAsync(version, CatalogFixtures.English);
        return catalog.PathOf(catalog.Items.Find(item)!);
    }

    // A reader holding the en_US catalogs of the given versions; any other is pending.
    private static async Task<FakeReader> ReaderAsync(params PatchVersion[] stored)
    {
        var catalogs = new Dictionary<PatchVersion, CatalogSnapshot>();
        foreach (var version in stored)
        {
            catalogs[version] = await CatalogFixtures.ReadAsync(version, CatalogFixtures.English);
        }

        return new FakeReader(catalogs);
    }

    private sealed class FakeReader(Dictionary<PatchVersion, CatalogSnapshot> catalogs)
        : ICatalogReader
    {
        public List<PatchVersion> Reads { get; } = [];

        public List<DdragonLanguage> Languages { get; } = [];

        public Task<CatalogVersions> GetVersionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CatalogVersions { Latest = Latest, Listed = Listed, Ready = [] });

        public Task<PatchVersion?> GetLatestAsync(CancellationToken cancellationToken) =>
            Task.FromResult<PatchVersion?>(Latest);

        public Task<DateTimeOffset?> GetLatestPromotedAtAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<DateTimeOffset?>(null);

        public Task<IReadOnlyList<DdragonLanguage>> GetLanguagesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DdragonLanguage>>([DdragonLanguage.EnUs]);

        public Task<CatalogLoad> GetAsync(
            PatchVersion version,
            DdragonLanguage language,
            ColdDemand demand,
            CancellationToken cancellationToken)
        {
            Reads.Add(version);
            Languages.Add(language);
            return Task.FromResult(catalogs.TryGetValue(version, out var catalog)
                ? CatalogLoad.Ready(catalog)
                : CatalogLoad.Pending(refused: false));
        }
    }
}
