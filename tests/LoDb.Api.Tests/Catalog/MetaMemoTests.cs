using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Meta;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Testing.States;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// <c>/api/meta</c> serializes and tags its answer once while the lists it is built from
/// read the same, and tags it as a fresh serialization would; the oldest page of changes
/// follows the index of states.
/// </summary>
public sealed class MetaMemoTests
{
    private static readonly JsonOptions Json = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SameListsServeTheSameBody()
    {
        var reader = new ListsReader();
        var endpoint = Endpoint(reader);

        var first = await BodyAsync(endpoint);
        var second = await BodyAsync(endpoint);

        Assert.Same(first, second);
        Assert.Equal(first.Tag, (await BodyAsync(Endpoint(reader))).Tag);
    }

    [Fact]
    public async Task ChangedListsServeANewlyTaggedBody()
    {
        var reader = new ListsReader();
        var endpoint = Endpoint(reader);
        var before = await BodyAsync(endpoint);

        reader.Promote(PatchVersion.Parse("16.20.1"));
        var after = await BodyAsync(endpoint);

        Assert.NotEqual(before.Tag, after.Tag);
        Assert.Equal((await BodyAsync(Endpoint(reader))).Tag, after.Tag);
    }

    // The index covers 16.17.1 to 16.19.1: the first covered version has no page of changes,
    // and the newest is the coverage end, behind the latest version, 16.20.1, while the index
    // lags behind its promotion.
    [Fact]
    public async Task PagesOfChangesFollowTheIndex()
    {
        var states = new InMemoryEntityStateIndex();
        var reader = new ListsReader();
        reader.Promote(PatchVersion.Parse("16.20.1"));
        var endpoint = Endpoint(reader, states);
        var before = await MetaAsync(endpoint);

        states.Snapshot = TestStates.Snapshot(["16.17.1", "16.18.1", "16.19.1"], []);
        var covered = await MetaAsync(endpoint);
        states.IsUnavailable = true;
        var unreadable = await MetaAsync(endpoint);

        Assert.Equal((null, null), (before.OldestChangesVersion, before.NewestChangesVersion));
        Assert.Equal(
            ("16.18.1", "16.19.1"),
            (covered.OldestChangesVersion, covered.NewestChangesVersion));
        Assert.Equal(
            (null, null),
            (unreadable.OldestChangesVersion, unreadable.NewestChangesVersion));
    }

    private static MetaEndpoint Endpoint(
        ListsReader reader,
        InMemoryEntityStateIndex? states = null) =>
        new(
            reader,
            states ?? new InMemoryEntityStateIndex(),
            Options.Create(Json),
            NullLogger<MetaEndpoint>.Instance);

    private static async Task<CatalogMeta> MetaAsync(MetaEndpoint endpoint)
    {
        var answer = await endpoint.GetAsync(Token);
        return Assert.IsType<CachedJson<CatalogMeta>>(answer.Result).Value;
    }

    private static async Task<JsonBody> BodyAsync(MetaEndpoint endpoint)
    {
        var answer = await endpoint.GetAsync(Token);
        var json = Assert.IsType<CachedJson<CatalogMeta>>(answer.Result);
        return Assert.IsType<JsonBody>(json.Body);
    }

    // Lists rebuilt on every read, as the reader parses them from its caches.
    private sealed class ListsReader : ICatalogReader
    {
        private string[] versions = ["16.19.1", "16.18.1"];

        public void Promote(PatchVersion version) => versions = [version.Value, .. versions];

        public Task<CatalogVersions> GetVersionsAsync(CancellationToken cancellationToken)
        {
            List<PatchVersion> listed = [.. versions.Select(PatchVersion.Parse)];
            return Task.FromResult(new CatalogVersions
            {
                Latest = listed[0],
                Listed = listed,
                Ready = [listed[0]],
            });
        }

        public Task<IReadOnlyList<DdragonLanguage>> GetLanguagesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DdragonLanguage>>(
                [DdragonLanguage.EnUs, DdragonLanguage.Parse("fr_FR")]);

        public Task<PatchVersion?> GetLatestAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DateTimeOffset?> GetLatestPromotedAtAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CatalogLoad> GetAsync(
            PatchVersion version,
            DdragonLanguage language,
            ColdDemand demand,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
