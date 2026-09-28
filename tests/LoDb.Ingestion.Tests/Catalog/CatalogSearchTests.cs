using LoDb.Domain.Catalog;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The search of a catalog, with the legacy rules: bounds, accents and case ignored, items
/// by name alone, debris never found, hits resource by resource in the upstream order.
/// </summary>
public sealed class CatalogSearchTests
{
    private static readonly ResourceType[] AllTypes = [];

    // One emoji is two UTF-16 units but one character, as mb_strlen counts it.
    public static TheoryData<string?> OutOfBounds =>
    [
        (string?)null,
        string.Empty,
        "a",
        "  a  ",
        "\U0001F642",
        new string('a', SearchQuery.MaxLength + 1),
    ];

    [Theory]
    [MemberData(nameof(OutOfBounds))]
    public void QueryOutOfBoundsIsRefused(string? text)
    {
        Assert.False(SearchQuery.TryParse(text, out var query));
        Assert.Null(query);
        Assert.Throws<FormatException>(() => SearchQuery.Parse(text!));
    }

    [Theory]
    [InlineData("ab", "ab")]
    [InlineData("  Épée  ", "Épée")]
    [InlineData("\U0001F642\U0001F642", "\U0001F642\U0001F642")]
    public void QueryIsTrimmedAndCountedInCharacters(string text, string expected)
    {
        Assert.True(SearchQuery.TryParse(text, out var query));
        Assert.Equal(expected, query.Text);
    }

    [Fact]
    public void LongestQueryIsAccepted()
    {
        var text = new string('a', SearchQuery.MaxLength);

        Assert.Equal(text, SearchQuery.Parse($" {text} ").Text);
    }

    [Fact]
    public async Task AccentsAndCaseAreIgnored()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.French);

        var hits = catalog.Search(SearchQuery.Parse("FEERIQUE"), AllTypes, 10);
        var trinity = Assert.Single(catalog.Search(SearchQuery.Parse("trinite"), AllTypes, 10));

        Assert.Equal(
            [(ResourceType.Items, "1004"), (ResourceType.Items, "771004")],
            hits.Select(static hit => (hit.Type, hit.Id)));
        Assert.All(hits, static hit => Assert.Equal("Charme féérique", hit.Name));
        Assert.Equal("Force de la trinité", trinity.Name);
        Assert.Equal("items/3078-trinity-force", trinity.Path.Value);
    }

    [Fact]
    public async Task ItemsMatchOnTheirNameAloneTheOthersOnTheirIdToo()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.English);

        Assert.Empty(catalog.Search(SearchQuery.Parse("3078"), AllTypes, 10));
        Assert.Equal(
            [(ResourceType.Runes, "8000")],
            Ids(catalog.Search(SearchQuery.Parse("8000"), AllTypes, 10)));
        Assert.Equal(
            [(ResourceType.Champions, "MonkeyKing")],
            Ids(catalog.Search(SearchQuery.Parse("monkey"), AllTypes, 10)));
        Assert.Equal(
            [
                (ResourceType.Summoners, "SummonerFlash_Jade"),
                (ResourceType.Summoners, "SummonerHeal_Jade"),
            ],
            Ids(catalog.Search(SearchQuery.Parse("jade"), AllTypes, 10)));
    }

    [Fact]
    public async Task DebrisIsNeverFound()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.English);

        Assert.Empty(catalog.Search(SearchQuery.Parse("placeholder"), AllTypes, 10));
        Assert.Empty(catalog.Search(SearchQuery.Parse("gangplank"), AllTypes, 10));
    }

    [Fact]
    public async Task HitsComeResourceByResourceWithinTheLimit()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.English);
        var query = SearchQuery.Parse("ar");

        var all = catalog.Search(query, AllTypes, 10);
        var first = catalog.Search(query, AllTypes, 1);
        var items = catalog.Search(query, [ResourceType.Items], 2);

        Assert.Equal(
            [
                (ResourceType.Champions, "Garen"),
                (ResourceType.Items, "1004"),
                (ResourceType.Items, "3599"),
                (ResourceType.Items, "771004"),
                (ResourceType.Summoners, "SummonerSnowball"),
            ],
            Ids(all));
        Assert.Equal(
            [
                (ResourceType.Champions, "Garen"),
                (ResourceType.Items, "1004"),
                (ResourceType.Summoners, "SummonerSnowball"),
            ],
            Ids(first));
        Assert.Equal([(ResourceType.Items, "1004"), (ResourceType.Items, "3599")], Ids(items));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Search(query, AllTypes, 0));
    }

    private static IEnumerable<(ResourceType Type, string Id)> Ids(IEnumerable<SearchHit> hits) =>
        hits.Select(static hit => (hit.Type, hit.Id));
}
