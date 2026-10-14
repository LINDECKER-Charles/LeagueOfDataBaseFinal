using LoDb.Domain.Languages;
using LoDb.Domain.Paths;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;

namespace LoDb.Ingestion.Catalog.Canonicals;

/// <summary>Reads the <see cref="CanonicalReferences"/> of a version.</summary>
/// <remarks>
/// The latest version's en_US catalog is pinned in memory (<see cref="ICatalogPinning"/>) and
/// its version read from the store. The next version's is read as the caller's
/// <see cref="ColdDemand"/> allows, like any catalog, and only when the latest one may lack an
/// entity: for every page of a version older than the latest one but the one just before it,
/// for a single page only when the latest version lacks its entity.
/// </remarks>
public sealed class CanonicalReader(ICatalogReader reader)
{
    /// <summary>The references of every page of a version: what its sitemap lists.</summary>
    /// <returns>
    /// The references, or <see langword="null"/> while one of their catalogs is pending: not
    /// stored yet, or its load refused. A catalog Data Dragon does not list holds nothing.
    /// </returns>
    /// <exception cref="Egress.Errors.EgressException">
    /// Data Dragon failed: the list of versions, or a synchronous ingestion.
    /// </exception>
    public async Task<CanonicalReferences?> ReadAsync(
        PatchVersion version,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        var references = await WithLatestAsync(version, demand, cancellationToken);
        return references is not null && IsOlderThanLatest(references)
            ? await WithNextAsync(references, demand, cancellationToken)
            : references;
    }

    /// <summary>
    /// Where the page of the entity <paramref name="path"/> names, in
    /// <paramref name="version"/>, is canonical.
    /// </summary>
    /// <returns>
    /// The location, or <see langword="null"/> while a catalog it reads is pending.
    /// </returns>
    /// <exception cref="Egress.Errors.EgressException">
    /// Data Dragon failed: the list of versions, or a synchronous ingestion.
    /// </exception>
    public async Task<CanonicalLocation?> LocateAsync(
        PatchVersion version,
        CanonicalPath path,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(path);
        var references = await WithLatestAsync(version, demand, cancellationToken);
        if (references is not null
            && IsOlderThanLatest(references)
            && references.LatestCatalog?.PageOf(path) is null)
        {
            references = await WithNextAsync(references, demand, cancellationToken);
        }

        return references is null ? null : new CanonicalLocation(references.Locate(path));
    }

    private static bool IsOlderThanLatest(CanonicalReferences references) =>
        references.Latest is { } latest && references.Version < latest;

    // The latest version's catalog stands for the next one until a version between them is
    // read: none for the latest version itself, nor before any promotion.
    private async Task<CanonicalReferences?> WithLatestAsync(
        PatchVersion version,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        var latest = await reader.GetLatestAsync(cancellationToken);
        var references = new CanonicalReferences { Version = version, Latest = latest };
        if (latest is null || version == latest)
        {
            return references;
        }

        var current = await EnglishAsync(latest, demand, cancellationToken);
        return current.Status == CatalogLoadStatus.Pending
            ? null
            : references with { LatestCatalog = current.Catalog, NextCatalog = current.Catalog };
    }

    private async Task<CanonicalReferences?> WithNextAsync(
        CanonicalReferences references,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        var versions = await reader.GetVersionsAsync(cancellationToken);
        var next = NextOf(versions.Listed, references.Version);
        if (next is null || next >= references.Latest)
        {
            return references;
        }

        var following = await EnglishAsync(next, demand, cancellationToken);
        return following.Status == CatalogLoadStatus.Pending
            ? null
            : references with { NextCatalog = following.Catalog };
    }

    private Task<CatalogLoad> EnglishAsync(
        PatchVersion version,
        ColdDemand demand,
        CancellationToken cancellationToken) =>
        reader.GetAsync(version, DdragonLanguage.EnUs, demand, cancellationToken);

    // Data Dragon lists the newest first: the version listed after this one comes before it.
    private static PatchVersion? NextOf(IReadOnlyList<PatchVersion> listed, PatchVersion version)
    {
        for (var index = 1; index < listed.Count; index++)
        {
            if (listed[index] == version)
            {
                return listed[index - 1];
            }
        }

        return null;
    }
}
