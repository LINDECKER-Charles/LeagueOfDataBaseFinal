using System.Net;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// How long each answer may be reused: a day for a version older than the latest, a minute
/// served stale while it revalidates for the latest, and a strong tag on every answer.
/// </summary>
[Collection(CatalogApiGroup.Name)]
public sealed class CacheHeaderTests(CatalogApiFixture api)
{
    private const string Frozen = "public, max-age=86400";
    private const string Current = "public, max-age=60, stale-while-revalidate=600";

    /// <summary>The header as written, not as <see cref="HttpClient"/> reformats it.</summary>
    internal static string CacheControlOf(HttpResponseMessage response) =>
        response.Headers.NonValidated["Cache-Control"].ToString();

    [Theory]
    [InlineData("/api/catalog/16.19.1/en_US/champions")]
    [InlineData("/api/catalog/16.19.1/en_US/items/1004")]
    [InlineData("/api/pickers/runes?version=16.19.1&lang=en_US")]
    [InlineData("/api/meta")]
    public async Task LatestVersionIsShortLivedAndRevalidated(string path)
    {
        using var response = await api.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Current, CacheControlOf(response));
        Assert.Null(response.Headers.RetryAfter);
    }

    [Theory]
    [InlineData("/api/catalog/7.21.1/en_US/runes")]
    [InlineData("/api/catalog/16.18.1/en_US/runes/8000-precision")]
    public async Task OlderVersionIsKeptADay(string path)
    {
        using var response = await api.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Frozen, CacheControlOf(response));
    }

    [Fact]
    public async Task ListIsTaggedAndRevalidated()
    {
        const string Path = "/api/catalog/16.19.1/en_US/items";
        using var first = await api.GetAsync(Path);
        var tag = first.Headers.ETag?.ToString();
        Assert.NotNull(tag);
        using var again = await api.GetAsync(Path);
        using var held = await api.GetAsync(Path, tag);
        using var stale = await api.GetAsync(Path, "\"another\"");

        Assert.False(first.Headers.ETag!.IsWeak);
        Assert.Equal(tag, again.Headers.ETag?.ToString());
        Assert.Equal(HttpStatusCode.NotModified, held.StatusCode);
        Assert.Equal(Current, CacheControlOf(held));
        Assert.Empty(await held.Content.ReadAsByteArrayAsync(CatalogApiFixture.Token));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
    }

    [Fact]
    public async Task EachLanguageHasItsOwnTag()
    {
        using var english = await api.GetAsync("/api/catalog/16.19.1/en_US/champions");
        using var french = await api.GetAsync("/api/catalog/16.19.1/fr_FR/champions");

        Assert.NotEqual(english.Headers.ETag, french.Headers.ETag);
    }

    [Fact]
    public async Task FailureCarriesNoCacheOfItsOwn()
    {
        using var response = await api.GetAsync("/api/catalog/99.1.1/en_US/champions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.ETag);
        Assert.Null(response.Headers.RetryAfter);
    }
}
