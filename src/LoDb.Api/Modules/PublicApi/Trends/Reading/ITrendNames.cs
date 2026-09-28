namespace LoDb.Api.Modules.PublicApi.Trends.Reading;

/// <summary>The display names of the ranked entities.</summary>
internal interface ITrendNames
{
    /// <summary>
    /// The names of the entities of <paramref name="type"/> in the latest catalog in en_US,
    /// by id; empty until a first version is ingested.
    /// </summary>
    /// <exception cref="Http.DependencyUnavailableException">
    /// The latest catalog cannot be read from the storage.
    /// </exception>
    Task<IReadOnlyDictionary<string, string>> NamesAsync(
        TrendType type,
        CancellationToken cancellationToken);
}
