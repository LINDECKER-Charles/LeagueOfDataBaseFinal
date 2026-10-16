using LoDb.Domain.Catalog;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Languages;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Pipeline.Datasets;
using LoDb.Ingestion.Tests.Ddragon;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// Placeholder summoner spells in a catalog: the recording ships none, so they are added to
/// the stored datasets, as Data Dragon ships them from some version on.
/// </summary>
public sealed class CatalogSummonerDebrisTests
{
    private const string IdOnly = "Summoner_UltBookPlaceholder";
    private const string TranslatedOnly = "SummonerSlot";
    private static readonly ResourceType[] AllTypes = [];

    [Fact]
    public async Task PlaceholderSpellsAreLeftOutOfTheListedSpells()
    {
        var catalog = await BuildAsync(CatalogFixtures.English);

        Assert.DoesNotContain(catalog.ListedSummoners, static spell => IsAdded(spell.Id));
        Assert.Equal(catalog.Summoners.Entries.Count - 2, catalog.ListedSummoners.Count);
        Assert.Contains(catalog.ListedSummoners, static spell => spell.Id == "SummonerFlash_Jade");
        Assert.NotNull(catalog.Summoners.Find(IdOnly));
    }

    [Fact]
    public async Task APlaceholderMarkedInItsIdAloneIsNotListed()
    {
        var catalog = await BuildAsync(CatalogFixtures.French);

        Assert.False(catalog.IsListed(catalog.Summoners.Find(IdOnly)!));
        Assert.True(catalog.IsListed(catalog.Summoners.Find("SummonerFlash_Jade")!));
    }

    [Fact]
    public async Task ATranslatedPlaceholderIsNotListedByItsEnglishName()
    {
        var catalog = await BuildAsync(CatalogFixtures.French);
        var translated = catalog.Summoners.Find(TranslatedOnly)!;

        Assert.DoesNotContain("Placeholder", translated.Name, StringComparison.Ordinal);
        Assert.False(catalog.IsListed(translated));
    }

    [Theory]
    [InlineData(IdOnly)]
    [InlineData(TranslatedOnly)]
    public async Task APlaceholderHasNoPageAndIsNeverFound(string id)
    {
        var catalog = await BuildAsync(CatalogFixtures.French);
        var path = catalog.PathOf(catalog.Summoners.Find(id)!);

        Assert.Null(catalog.PageOf(path));
        Assert.Empty(catalog.Search(SearchQuery.Parse(id), AllTypes, 10));
        Assert.Empty(catalog.Search(SearchQuery.Parse("placeholder"), AllTypes, 10));
    }

    [Fact]
    public async Task AnOrdinarySpellKeepsItsPageAndItsSearch()
    {
        var catalog = await BuildAsync(CatalogFixtures.French);
        var flash = catalog.Summoners.Find("SummonerFlash_Jade")!;

        Assert.Equal(catalog.PathOf(flash), catalog.PageOf(catalog.PathOf(flash)));
        Assert.Contains(
            catalog.Search(SearchQuery.Parse("jade"), AllTypes, 10),
            static hit => hit.Id == "SummonerFlash_Jade");
    }

    private static bool IsAdded(string id) => id is IdOnly or TranslatedOnly;

    private static async Task<CatalogSnapshot> BuildAsync(DdragonLanguage language)
    {
        using var harness = ReplayHarness.Create();
        var version = CatalogFixtures.Latest;
        var english = new CatalogSnapshot(
            version,
            CatalogFixtures.English,
            await WithPlaceholdersAsync(harness, CatalogFixtures.English),
            null);
        return language == CatalogFixtures.English
            ? english
            : new CatalogSnapshot(
                version,
                language,
                await WithPlaceholdersAsync(harness, language),
                english);
    }

    // The id-only spell keeps a neutral name in every language; the other is named
    // "Placeholder" in en_US alone, as Data Dragon translates it in some languages.
    private static async Task<VersionDatasets> WithPlaceholdersAsync(
        ReplayHarness harness,
        DdragonLanguage language)
    {
        var datasets = await CatalogFixtures.DatasetsAsync(
            harness,
            CatalogFixtures.Latest,
            language);
        var translatedName = language == CatalogFixtures.English ? "Placeholder" : "Emplacement";
        return datasets with
        {
            Summoners = datasets.Summoners with
            {
                Entries =
                [
                    .. datasets.Summoners.Entries,
                    Spell(IdOnly, "Ultimate slot"),
                    Spell(TranslatedOnly, translatedName),
                ],
            },
        };
    }

    private static SummonerSpell Spell(string id, string name) => new()
    {
        Id = id,
        Key = "0",
        Name = name,
        Description = string.Empty,
        Image = id + ".png",
        Cooldown = [0],
        Cost = [0],
        Range = [0],
        Modes = ["ULTBOOK"],
    };
}
