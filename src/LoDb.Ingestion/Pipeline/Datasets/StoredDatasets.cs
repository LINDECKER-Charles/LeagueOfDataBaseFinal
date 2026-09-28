using LoDb.Infrastructure.Storage.Datasets;
using LoDb.Ingestion.Normalization;
using LoDb.Ingestion.Normalization.Serialization;

namespace LoDb.Ingestion.Pipeline.Datasets;

/// <summary>
/// Reads the datasets of a (version, language) back from the <see cref="IDatasetStore"/>.
/// </summary>
internal sealed class StoredDatasets(IDatasetStore store)
{
    /// <returns>
    /// The four datasets, or <see langword="null"/> while one of them is not stored yet.
    /// </returns>
    public async Task<VersionDatasets?> LoadAsync(
        DatasetScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var champions = await ReadAsync(scope, DatasetTypes.Champions, cancellationToken)
            .ConfigureAwait(false);
        var items = await ReadAsync(scope, DatasetTypes.Items, cancellationToken)
            .ConfigureAwait(false);
        var runes = await ReadAsync(scope, DatasetTypes.Runes, cancellationToken)
            .ConfigureAwait(false);
        var summoners = await ReadAsync(scope, DatasetTypes.Summoners, cancellationToken)
            .ConfigureAwait(false);
        return champions is null || items is null || runes is null || summoners is null
            ? null
            : new VersionDatasets
            {
                Champions = champions,
                Items = items,
                Runes = runes,
                Summoners = summoners,
            };
    }

    /// <summary>Whether the four datasets are stored, without reading them.</summary>
    public async Task<bool> ExistAsync(DatasetScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        DatasetKey[] keys =
        [
            scope.Key(DatasetTypes.Champions),
            scope.Key(DatasetTypes.Items),
            scope.Key(DatasetTypes.Runes),
            scope.Key(DatasetTypes.Summoners),
        ];
        foreach (var key in keys)
        {
            if (!await store.ExistsAsync(key, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<DatasetDocument<TEntry>?> ReadAsync<TEntry>(
        DatasetScope scope,
        DatasetType<TEntry> type,
        CancellationToken cancellationToken)
    {
        var json = await store.OpenReadAsync(scope.Key(type), cancellationToken)
            .ConfigureAwait(false);
        if (json is null)
        {
            return null;
        }

        await using (json.ConfigureAwait(false))
        {
            return await DatasetSerializer.DeserializeAsync(type, json, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
