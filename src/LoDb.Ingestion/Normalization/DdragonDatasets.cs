using LoDb.Domain.Catalog.Champions;
using LoDb.Domain.Catalog.Items;
using LoDb.Domain.Catalog.Runes;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Ddragon;
using LoDb.Ingestion.Egress;
using LoDb.Ingestion.Normalization.Mapping;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Normalization;

/// <inheritdoc />
internal sealed class DdragonDatasets(IEgressFetcher fetcher, IOptions<EgressOptions> options)
    : IDdragonDatasets
{
    public async Task<ChromaCatalog> ReadChromasAsync(
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        using var batch = fetcher.OpenBatch();
        foreach (var patch in ChromaPatches(version))
        {
            var skins = await UpstreamDocuments
                .ReadAsync(batch, RawDocuments.Skins(patch), cancellationToken)
                .ConfigureAwait(false);
            if (skins is not null)
            {
                return ChromaMapper.Catalog(patch, skins);
            }
        }

        return ChromaCatalog.Empty;
    }

    public Task<DatasetDocument<ChampionDetail>> ReadChampionsAsync(
        DatasetScope scope,
        ChromaCatalog chromas,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(chromas);
        var source = new ChampionSource(scope.Version, options.Value.FetchConcurrency);
        return ReadAsync(
            scope,
            async (batch, language, token) =>
                await source.ReadAsync(batch, language, token).ConfigureAwait(false) is { } raw
                    ? ChampionMapper.Map(raw, chromas)
                    : null,
            cancellationToken);
    }

    public Task<DatasetDocument<Item>> ReadItemsAsync(
        DatasetScope scope,
        CancellationToken cancellationToken) =>
        ReadAsync(
            scope,
            async (batch, language, token) =>
                await UpstreamDocuments
                    .ReadAsync(batch, RawDocuments.Items(scope.Version, language), token)
                    .ConfigureAwait(false) is { } file
                    ? ItemMapper.Map(file)
                    : null,
            cancellationToken);

    public Task<DatasetDocument<RuneTree>> ReadRunesAsync(
        DatasetScope scope,
        CancellationToken cancellationToken) =>
        ReadAsync(
            scope,
            async (batch, language, token) =>
                await UpstreamDocuments
                    .ReadAsync(batch, RawDocuments.RuneTrees(scope.Version, language), token)
                    .ConfigureAwait(false) is { } trees
                    ? RuneMapper.Map(trees)
                    : null,
            cancellationToken);

    public Task<DatasetDocument<SummonerSpell>> ReadSummonersAsync(
        DatasetScope scope,
        CancellationToken cancellationToken) =>
        ReadAsync(
            scope,
            async (batch, language, token) =>
                await UpstreamDocuments
                    .ReadAsync(batch, RawDocuments.SummonerSpells(scope.Version, language), token)
                    .ConfigureAwait(false) is { } file
                    ? SummonerSpellMapper.Map(file)
                    : null,
            cancellationToken);

    private static IEnumerable<string> ChromaPatches(PatchVersion version) =>
        [CommunityDragonUrls.Patch(version), CommunityDragonUrls.LatestPatch];

    // The requested language, then en_US, then an empty document (UP 1, UP 2). A read that
    // throws leaves no document at all: nothing half-read can be persisted.
    private async Task<DatasetDocument<TEntry>> ReadAsync<TEntry>(
        DatasetScope scope,
        Func<IEgressBatch, DdragonLanguage, CancellationToken, Task<IReadOnlyList<TEntry>?>> read,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        using var batch = fetcher.OpenBatch();
        foreach (var language in LanguageFallback.Chain(scope.Language))
        {
            if (await read(batch, language, cancellationToken).ConfigureAwait(false) is { } entries)
            {
                return Document(scope, language, entries);
            }
        }

        return Document<TEntry>(scope, null, []);
    }

    private static DatasetDocument<TEntry> Document<TEntry>(
        DatasetScope scope,
        DdragonLanguage? contentLanguage,
        IReadOnlyList<TEntry> entries) => new()
    {
        Version = scope.Version.Value,
        Language = scope.Language.Code,
        ContentLanguage = contentLanguage?.Code,
        Entries = entries,
    };
}
