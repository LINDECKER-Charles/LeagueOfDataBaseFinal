using System.Net;
using System.Text.Json;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Legacy;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoDb.Api.Tests.Legacy;

/// <summary>
/// An old URL never ingests: the latest catalog answers first, the catalog of the version
/// it names only once stored, and a name neither knows, in a version not stored, lands on the
/// list of its version instead of a 404, once the latest catalog could be read. 16.19.1 is
/// stored and latest; 16.18.1 never is; 7.22.1 is stored by a catalog call first.
/// </summary>
[Collection(LegacyApiGroup.Name)]
public sealed class LegacyStoredOnlyTests(LegacyApiFixture api)
{
    private const string NeverStored = "16.18.1";

    [Fact]
    public async Task OldUrlOfAVersionNotStoredIngestsNothing()
    {
        var target = await api.LocationOfAsync($"/{NeverStored}/champion/Garen");
        using var unknown = await api.SendAsync($"/{NeverStored}/champion/NotAChampion");

        Assert.Equal($"/en/{NeverStored}/champions/Garen", target);
        Assert.Equal(HttpStatusCode.Found, unknown.StatusCode);
        Assert.Equal($"/en/{NeverStored}/champions", unknown.Headers.Location?.OriginalString);
        Assert.Equal(TimeSpan.FromHours(1), unknown.Headers.CacheControl?.MaxAge);
        Assert.DoesNotContain(
            api.UpstreamRequests,
            static url => url.AbsolutePath.Contains($"/{NeverStored}/", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(api.StorageRoot, "data", NeverStored)));
    }

    [Fact]
    public async Task EntryGoneFromTheGameLandsOnTheListOfItsVersion()
    {
        using var response = await api.SendAsync($"/{NeverStored}/object/9999?lang=fr_FR");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal($"/fr/{NeverStored}/items", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UnreadLatestCatalogDefersInsteadOfLandingOnTheList()
    {
        var reader = new LatestUnread(api.Services.GetRequiredService<ICatalogReader>());
        var resolver = new LegacyResolver(reader, NullLogger<LegacyResolver>.Instance);

        var answer = await resolver.ResolveAsync(
            new LegacyRequest($"{NeverStored}/champion/Garen", null, null),
            LegacyApiFixture.Token);

        var problem = Assert.IsType<CatalogProblem>(answer.Result);
        Assert.Equal(CatalogProblem.Pending().Code, problem.Code);
    }

    [Fact]
    public async Task StoredVersionTellsANameTheLatestLacks()
    {
        using var stored = await api.GetApiAsync("/api/catalog/7.22.1/en_US/runes");
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);

        using var response = await api.SendAsync("/7.22.1/rune/NotARune");
        var body = await response.Content.ReadAsStringAsync(LegacyApiFixture.Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "unknown-entity",
            JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
    }

    // The latest catalog not readable for now, as when the cold gate refuses its load: it
    // answers what a stored-only read of a version never stored does.
    private sealed class LatestUnread(ICatalogReader inner) : ICatalogReader
    {
        public Task<CatalogVersions> GetVersionsAsync(CancellationToken cancellationToken) =>
            inner.GetVersionsAsync(cancellationToken);

        public Task<PatchVersion?> GetLatestAsync(CancellationToken cancellationToken) =>
            inner.GetLatestAsync(cancellationToken);

        public Task<IReadOnlyList<DdragonLanguage>> GetLanguagesAsync(
            CancellationToken cancellationToken) =>
            inner.GetLanguagesAsync(cancellationToken);

        public Task<CatalogLoad> GetAsync(
            PatchVersion version,
            DdragonLanguage language,
            ColdDemand demand,
            CancellationToken cancellationToken) =>
            version == DdragonFixtures.Latest
                ? inner.GetAsync(
                    PatchVersion.Parse(NeverStored),
                    language,
                    ColdDemand.StoredOnly,
                    cancellationToken)
                : inner.GetAsync(version, language, demand, cancellationToken);
    }
}
