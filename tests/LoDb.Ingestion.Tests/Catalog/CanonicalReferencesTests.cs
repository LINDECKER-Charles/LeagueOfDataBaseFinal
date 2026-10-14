using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Canonicals;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// Where the pages of a version are canonical, over the recorded catalogs: the latest short
/// URL for an entity the latest version holds, else the page of the last version holding it,
/// and no canonical in the versions before. Molten Edge (3371) is in 7.22.1 and 8.7.1, gone
/// from 16.18.1 on.
/// </summary>
public sealed class CanonicalReferencesTests
{
    private const string MoltenEdge = "3371";

    private static readonly PatchVersion Latest = CatalogFixtures.Latest;
    private static readonly PatchVersion Previous = PatchVersion.Parse("16.18.1");
    private static readonly PatchVersion Archived = PatchVersion.Parse("8.7.1");
    private static readonly PatchVersion Older = PatchVersion.Parse("7.22.1");
    private static readonly PatchVersion Season3 = PatchVersion.Parse("3.13.24");

    [Fact]
    public async Task EntityOfTheLatestVersionIsIndexedUnderItsLatestPath()
    {
        var references = await ReferencesAsync(Archived, Previous);
        var boots = await ItemPathAsync(Archived, "1001");

        var page = references.Locate(boots);

        Assert.Equal("items/1001-boots-of-speed", boots.Value);
        Assert.Null(page?.Version);
        Assert.Equal("items/1001-boots", page?.Path.Value);
    }

    [Fact]
    public async Task ChampionIdThatChangedItsCaseIsStillFoundAtTheLatestVersion()
    {
        var references = await ReferencesAsync(Season3, Older);

        var page = references.Locate(CanonicalPath.Champion("FiddleSticks"));

        Assert.Null(page?.Version);
        Assert.Equal("champions/Fiddlesticks", page?.Path.Value);
    }

    [Fact]
    public async Task DroppedEntityIsIndexedInTheLastVersionThatHeldIt()
    {
        var references = await ReferencesAsync(Archived, Previous);
        var moltenEdge = await ItemPathAsync(Archived, MoltenEdge);

        var page = references.Locate(moltenEdge);

        Assert.Equal(Archived, page?.Version);
        Assert.Equal(moltenEdge, page?.Path);
        Assert.True(references.IsOwnCanonical(moltenEdge));
    }

    [Fact]
    public async Task DroppedEntityHasNoCanonicalBeforeItsLastVersion()
    {
        var references = await ReferencesAsync(Older, Archived);
        var moltenEdge = await ItemPathAsync(Older, MoltenEdge);

        Assert.Null(references.Locate(moltenEdge));
        Assert.False(references.IsOwnCanonical(moltenEdge));
    }

    [Fact]
    public async Task LatestVersionIsIndexedUnderItsOwnShortUrls()
    {
        var boots = await ItemPathAsync(Latest, "1001");
        var references = new CanonicalReferences { Version = Latest, Latest = Latest };

        var page = references.Locate(boots);

        Assert.Null(page?.Version);
        Assert.Equal(boots, page?.Path);
        Assert.False(references.IsOwnCanonical(boots));
    }

    [Fact]
    public void EveryPageIsItsOwnCanonicalBeforeAnyPromotion()
    {
        var references = new CanonicalReferences { Version = Archived };
        var garen = CanonicalPath.Champion("Garen");

        var page = references.Locate(garen);

        Assert.Equal(new CanonicalPage { Version = Archived, Path = garen }, page);
    }

    [Fact]
    public async Task EntityNewInAVersionNotPromotedYetIsIndexedThere()
    {
        var references = new CanonicalReferences
        {
            Version = PatchVersion.Parse("16.20.1"),
            Latest = Latest,
            LatestCatalog = await CatalogFixtures.ReadAsync(Latest, CatalogFixtures.English),
        };
        var newcomer = CanonicalPath.Champion("Newcomer");

        Assert.Equal(references.Version, references.Locate(newcomer)?.Version);
    }

    private static async Task<CanonicalReferences> ReferencesAsync(
        PatchVersion version,
        PatchVersion next) => new()
        {
            Version = version,
            Latest = Latest,
            LatestCatalog = await EnglishAsync(Latest),
            NextCatalog = await EnglishAsync(next),
        };

    private static async Task<CanonicalPath> ItemPathAsync(PatchVersion version, string id)
    {
        var catalog = await EnglishAsync(version);
        return catalog.PathOf(catalog.Items.Find(id)!);
    }

    private static Task<CatalogSnapshot> EnglishAsync(PatchVersion version) =>
        CatalogFixtures.ReadAsync(version, CatalogFixtures.English);
}
