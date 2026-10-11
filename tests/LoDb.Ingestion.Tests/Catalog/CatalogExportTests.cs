using System.Text;
using System.Text.Json.Nodes;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Storage.Blobs;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Export;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Tests.Queue;

namespace LoDb.Ingestion.Tests.Catalog;

/// <summary>
/// The projection the parity check compares: every entry in the upstream order with its
/// path, image and derived facts, written as UTF-8 JSON.
/// </summary>
public sealed class CatalogExportTests
{
    private static readonly BlobKey AhriBlob = BlobKey.ForContent("Ahri.png"u8, "png");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryResourceIsProjectedInTheUpstreamOrder()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.English);
        var resolver = new StubResolver();

        var projection = await ProjectAsync(catalog, resolver, ColdDemand.StoredOnly);

        Assert.Equal("16.19.1", (string?)projection["version"]);
        Assert.Equal("en_US", (string?)projection["language"]);
        Assert.Equal(
            ["Ahri", "Fiddlesticks", "Garen", "MonkeyKing", "Teemo"],
            Ids(projection, "champions"));
        Assert.Equal(18, Ids(projection, "items").Count);
        Assert.Equal(["8100", "8000"], Ids(projection, "runes"));
        Assert.Equal(6, Ids(projection, "summoners").Count);
        Assert.Equal("en_US", (string?)projection["items"]?["contentLanguage"]);
        Assert.Same(ColdDemand.StoredOnly, resolver.Demand);
        Assert.Equal(81, resolver.Requested.DistinctBy(Key).Count());
    }

    [Fact]
    public async Task ChampionCarriesItsDerivedFacts()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.French);

        var projection = await ProjectAsync(catalog, new StubResolver(), ColdDemand.Synchronous);

        var garen = Entry(projection, "champions", "Garen");
        var ahri = Entry(projection, "champions", "Ahri");
        Assert.Equal("champions/Garen", (string?)garen["path"]);
        Assert.Equal("none", (string?)garen["resource"]);
        Assert.Equal("melee", (string?)garen["attackRange"]);
        Assert.Equal("ranged", (string?)ahri["attackRange"]);
        Assert.Equal("mana", (string?)ahri["resource"]);
        Assert.Equal(4, ahri["spells"]?.AsArray().Count);
        Assert.Equal("Ahri.png", (string?)ahri["image"]?["file"]);
        Assert.NotNull(ahri["passive"]?["image"]);
        var skins = ahri["skins"]!.AsArray();
        var chromas = skins.SelectMany(static skin => skin!["chromas"]!.AsArray()).ToList();
        Assert.Equal(catalog.Champions.Find("Ahri")!.Skins.Count, skins.Count);
        Assert.NotEmpty(chromas);
        Assert.All(chromas, static chroma => Assert.NotNull((string?)chroma!["label"]));
    }

    [Fact]
    public async Task ItemCarriesItsEditionTwinTierAndStats()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.French);

        var projection = await ProjectAsync(catalog, new StubResolver(), ColdDemand.Synchronous);

        var trinity = Entry(projection, "items", "3078");
        var classicCharm = Entry(projection, "items", "771004");
        Assert.Equal("Force de la trinité", (string?)trinity["name"]);
        Assert.Equal("items/3078-trinity-force", (string?)trinity["path"]);
        Assert.Equal(("modern", true, "legendary"), Facts(trinity));
        Assert.Null(trinity["counterpart"]);
        Assert.Equal(
            """[{"stat":"attack_damage","value":36,"percent":false},"""
            + """{"stat":"attack_speed","value":0.3,"percent":true},"""
            + """{"stat":"health","value":333,"percent":false}]""",
            trinity["stats"]?.ToJsonString());
        Assert.Equal(("classic", true, "component"), Facts(classicCharm));
        Assert.Equal(
            """{"id":"1004","edition":"modern"}""",
            classicCharm["counterpart"]?.ToJsonString());
        Assert.Equal((false, (string?)null), Listing(Entry(projection, "items", "7050")));
    }

    [Fact]
    public async Task ImageCarriesItsStatus()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.English);
        var resolver = new StubResolver()
            .With(OnDemandIngestionTests.Ahri, ResolvedImage.Present(AhriBlob))
            .With(OnDemandIngestionTests.Garen, ResolvedImage.Absent);

        var projection = await ProjectAsync(catalog, resolver, ColdDemand.Synchronous);

        Assert.Equal(
            $$"""{"file":"Ahri.png","status":"present","url":"{{AhriBlob.PublicPath}}"}""",
            Entry(projection, "champions", "Ahri")["image"]?.ToJsonString());
        Assert.Equal(
            """{"file":"Garen.png","status":"absent","url":null}""",
            Entry(projection, "champions", "Garen")["image"]?.ToJsonString());
        Assert.Equal(
            """{"file":"Teemo.png","status":"pending","url":null}""",
            Entry(projection, "champions", "Teemo")["image"]?.ToJsonString());
    }

    [Fact]
    public async Task ProjectionIsWrittenAsPlainIndentedJson()
    {
        var catalog = await CatalogFixtures.LatestAsync(CatalogFixtures.French);
        var projection = await ProjectAsync(catalog, new StubResolver(), ColdDemand.Synchronous);
        using var output = new MemoryStream();

        await CatalogExport.WriteAsync(projection, output, Token);

        var text = Encoding.UTF8.GetString(output.ToArray());
        Assert.StartsWith("{\n  \"version\": \"16.19.1\",\n  \"language\": \"fr_FR\",", text);
        Assert.Contains("\"name\": \"Force de la trinité\"", text, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"Lance noire de Kalista\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.True(JsonNode.DeepEquals(projection, JsonNode.Parse(text)));
    }

    private static Task<JsonObject> ProjectAsync(
        CatalogSnapshot catalog,
        StubResolver resolver,
        ColdDemand demand) =>
        new CatalogExport(resolver).ProjectAsync(catalog, demand, Token);

    private static List<string> Ids(JsonObject projection, string resource) =>
        [.. projection[resource]!["entries"]!.AsArray().Select(static entry => entry!["id"]!
            .ToString())];

    private static JsonNode Entry(JsonObject projection, string resource, string id) =>
        projection[resource]!["entries"]!.AsArray()
            .Single(entry => entry!["id"]!.ToString() == id)!;

    private static (string?, bool?, string?) Facts(JsonNode item) =>
        ((string?)item["edition"], (bool?)item["listed"], (string?)item["tier"]);

    private static (bool?, string?) Listing(JsonNode item) =>
        ((bool?)item["listed"], (string?)item["tier"]);

    private static (string Type, string File) Key(DdragonImage image) =>
        (image.ManifestType, image.File);

    // Every image pending unless told otherwise; records what it was asked.
    private sealed class StubResolver : IImageResolver
    {
        private readonly Dictionary<string, ResolvedImage> verdicts = new(StringComparer.Ordinal);

        public IReadOnlyCollection<DdragonImage> Requested { get; private set; } = [];

        public ColdDemand? Demand { get; private set; }

        public StubResolver With(DdragonImage image, ResolvedImage verdict)
        {
            verdicts[image.File] = verdict;
            return this;
        }

        public Task<ImageResolution> ResolveAsync(
            PatchVersion version,
            IReadOnlyCollection<DdragonImage> images,
            ColdDemand demand,
            CancellationToken cancellationToken)
        {
            Requested = images;
            Demand = demand;
            var resolved = images.DistinctBy(Key).ToDictionary(
                Key,
                image => verdicts.GetValueOrDefault(image.File, ResolvedImage.Pending));
            return Task.FromResult(new ImageResolution(resolved, refused: false));
        }
    }
}
