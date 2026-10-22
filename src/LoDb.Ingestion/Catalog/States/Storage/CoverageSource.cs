using LoDb.Domain.Versions;
using LoDb.Ingestion.Pipeline.Datasets;
using LoDb.Ingestion.Pipeline.Versions;

namespace LoDb.Ingestion.Catalog.States.Storage;

/// <summary>
/// What decides the coverage of the index: the latest promoted version, the versions Data
/// Dragon lists, and the en_US datasets the store holds for them.
/// </summary>
internal sealed class CoverageSource(
    VersionStates versions,
    KnownReleases releases,
    StoredDatasets stored,
    DatasetIngestion ingestion)
{
    /// <summary>The newest promoted version; null before the first promotion.</summary>
    public Task<PatchVersion?> LatestAsync(CancellationToken cancellationToken) =>
        versions.LatestPromotedAsync(cancellationToken);

    /// <summary>
    /// Every listed version up to <paramref name="latest"/>, oldest first, the latest one
    /// included even when the cached list does not have it yet.
    /// </summary>
    /// <exception cref="Egress.Errors.EgressException">The list could not be read.</exception>
    public async Task<IReadOnlyList<PatchVersion>> ListedAsync(
        PatchVersion latest,
        CancellationToken cancellationToken)
    {
        var listed = await releases.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        return [.. listed.Append(latest).Where(version => version <= latest).Distinct().Order()];
    }

    /// <summary>
    /// Ingests the en_US datasets of <paramref name="wanted"/> the store lacks, one version
    /// after the other; a version Data Dragon does not ship is stored as definitively absent.
    /// </summary>
    /// <returns>The versions whose datasets were ingested.</returns>
    public async Task<int> IngestMissingAsync(
        IEnumerable<PatchVersion> wanted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        var ingested = 0;
        foreach (var scope in wanted.Select(EnglishEntities.ScopeOf))
        {
            if (await stored.ExistAsync(scope, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            await ingestion.IngestAsync(scope, cancellationToken).ConfigureAwait(false);
            ingested++;
        }

        return ingested;
    }

    /// <summary>
    /// The longest run of <paramref name="listed"/> versions (oldest first) whose en_US
    /// datasets are all stored, ending at the last one: the coverage, oldest first.
    /// </summary>
    public async Task<IReadOnlyList<PatchVersion>> StoredRunAsync(
        IReadOnlyList<PatchVersion> listed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listed);
        var start = listed.Count;
        while (start > 0 && await stored
                   .ExistAsync(EnglishEntities.ScopeOf(listed[start - 1]), cancellationToken)
                   .ConfigureAwait(false))
        {
            start--;
        }

        return [.. listed.Skip(start)];
    }
}
