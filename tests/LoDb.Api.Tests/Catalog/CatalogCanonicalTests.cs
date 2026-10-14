using System.Text.Json;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The page a detail names for search engines (ADR 0005, amendment of 2026-10-14): the latest
/// short URL of an entity the latest version holds. Where a dropped entity is indexed is
/// <c>CanonicalReferencesTests</c>' and the sitemaps': the recording holds no image of the
/// versions that had one, which a detail resolves before answering.
/// </summary>
[Collection(CatalogApiGroup.Name)]
public sealed class CatalogCanonicalTests(CatalogApiFixture api)
{
    private const string Canonical = "canonical";

    [Fact]
    public async Task LatestDetailIsItsOwnShortUrl()
    {
        var boots = await api.GetJsonAsync("/api/catalog/16.19.1/en_US/items/1001");

        Assert.Equal((null, "items/1001-boots"), LinkOf(boots));
    }

    [Fact]
    public async Task PastDetailOfAnEntityTheLatestVersionHoldsNamesItsShortUrl()
    {
        var garen = await api.GetJsonAsync("/api/catalog/16.18.1/fr_FR/champions/garen");
        var flash = await api.GetJsonAsync("/api/catalog/16.18.1/en_US/summoners/SummonerFlash");

        Assert.Equal((null, "champions/Garen"), LinkOf(garen));
        Assert.Equal((null, "summoners/SummonerFlash"), LinkOf(flash));
    }

    private static (string? Version, string Path) LinkOf(JsonElement details)
    {
        var link = details.GetProperty(Canonical);
        var version = link.GetProperty("version");
        return (
            version.ValueKind == JsonValueKind.Null ? null : version.GetString(),
            link.Text("path"));
    }
}
