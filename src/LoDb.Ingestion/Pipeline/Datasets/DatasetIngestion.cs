using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Storage.Datasets;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Normalization.Serialization;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Pipeline.Datasets;

/// <summary>
/// Reads, normalizes and stores the four datasets of the requested languages of a version.
/// </summary>
/// <remarks>
/// A stored dataset is never read again: a rerun only fetches what is missing. A dataset is
/// written whole or not at all, and a failure throws before anything half-read is stored
/// (<see cref="IDdragonDatasets"/>). The chromas are read once per run, and only if a
/// champions dataset is missing.
/// </remarks>
internal sealed class DatasetIngestion(
    IDdragonDatasets datasets,
    IDatasetStore store,
    IOptions<IngestionOptions> options)
{
    /// <returns>The number of datasets written; zero when every one was stored.</returns>
    public async Task<int> IngestAsync(
        PatchVersion version,
        IReadOnlyCollection<DdragonLanguage> languages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(languages);
        var chromas = new Lazy<Task<ChromaCatalog>>(
            () => datasets.ReadChromasAsync(version, cancellationToken));
        var parallelism = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Value.LanguageConcurrency,
            CancellationToken = cancellationToken,
        };
        var written = 0;
        await Parallel.ForEachAsync(
                languages,
                parallelism,
                async (language, token) =>
                {
                    var scope = new DatasetScope { Version = version, Language = language };
                    var count = await IngestLanguageAsync(scope, chromas, token)
                        .ConfigureAwait(false);
                    Interlocked.Add(ref written, count);
                })
            .ConfigureAwait(false);
        return written;
    }

    /// <summary>The four datasets of one language, for an on-demand request.</summary>
    public Task<int> IngestAsync(DatasetScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return IngestAsync(scope.Version, [scope.Language], cancellationToken);
    }

    private async Task<int> IngestLanguageAsync(
        DatasetScope scope,
        Lazy<Task<ChromaCatalog>> chromas,
        CancellationToken cancellationToken)
    {
        DatasetWrite[] writes =
        [
            Write(scope, DatasetTypes.Champions, async token => await datasets
                .ReadChampionsAsync(scope, await chromas.Value.ConfigureAwait(false), token)
                .ConfigureAwait(false)),
            Write(scope, DatasetTypes.Items, token => datasets.ReadItemsAsync(scope, token)),
            Write(scope, DatasetTypes.Runes, token => datasets.ReadRunesAsync(scope, token)),
            Write(
                scope,
                DatasetTypes.Summoners,
                token => datasets.ReadSummonersAsync(scope, token)),
        ];
        var written = 0;
        foreach (var write in writes)
        {
            if (await WriteAsync(write, cancellationToken).ConfigureAwait(false))
            {
                written++;
            }
        }

        return written;
    }

    private static DatasetWrite Write<TEntry>(
        DatasetScope scope,
        DatasetType<TEntry> type,
        Func<CancellationToken, Task<DatasetDocument<TEntry>>> read) =>
        new(
            scope.Key(type),
            async token => DatasetSerializer.Serialize(
                type,
                await read(token).ConfigureAwait(false)));

    private async Task<bool> WriteAsync(DatasetWrite write, CancellationToken cancellationToken)
    {
        if (await store.ExistsAsync(write.Key, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var json = await write.Read(cancellationToken).ConfigureAwait(false);
        return await store.WriteAsync(write.Key, json, cancellationToken).ConfigureAwait(false);
    }

    private sealed record DatasetWrite(
        DatasetKey Key,
        Func<CancellationToken, Task<byte[]>> Read);
}
