using LoDb.Api.Modules.Seo.Pages;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Catalog.States;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Pages;

/// <summary>
/// The older patches and their pages, read from the catalogs as the sitemaps read them
/// (<see cref="VersionPages"/>): the pass cannot drift from the links of the site. Their openers
/// come from the index of states, in memory.
/// </summary>
/// <remarks>
/// Paths do not depend on the language, so the en_US catalog lists them for all 21 locales.
/// The datasets of every version are stored ahead of the pass; one that is not gets queued
/// for ingestion as a task of the instance, and the unit waits for it. A command line, whose
/// host runs no worker, ingests it first and waits (<see cref="IngestAsync"/>).
/// </remarks>
internal sealed class CatalogPages(
    ICatalogReader reader,
    IEntityStateIndex index,
    IOptions<CatalogOptions> options) : IPrerenderCatalog
{
    private static readonly ColdDemand Demand = ColdDemand.Queued.By(ColdCaller.Internal);
    private static readonly ColdDemand Waited = ColdDemand.Synchronous.By(ColdCaller.Internal);

    // How long the SSR reuses its copy of /api/meta, which names the latest version: the
    // default of LODB_API_META_TTL_SECONDS.
    private static readonly TimeSpan SsrMetaLifetime = TimeSpan.FromMinutes(1);

    // Twice the API's cache of the versions, its local copy filled from the shared one at the
    // end of its life at worst, then the SSR's copy of the answer.
    public TimeSpan SettlingDelay => (2 * options.Value.VersionsLifetime) + SsrMetaLifetime;

    public async Task<IReadOnlyList<PatchVersion>> PinnedVersionsAsync(
        CancellationToken cancellationToken)
    {
        var versions = await reader.GetVersionsAsync(cancellationToken);
        return versions.Latest is { } latest
            ? [.. versions.Listed.Where(version => version < latest)]
            : [];
    }

    public async Task<PatchVersion?> LatestVersionAsync(CancellationToken cancellationToken) =>
        (await reader.GetVersionsAsync(cancellationToken)).Latest;

    public async Task<DateTimeOffset?> PinnedFromAsync(
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        var versions = await reader.GetVersionsAsync(cancellationToken);
        if (versions.Latest is not { } latest
            || version >= latest
            || !versions.Listed.Contains(version))
        {
            return null;
        }

        return IsLeaving(versions, version) && versions.LatestPromotedAt is { } promotedAt
            ? promotedAt + SettlingDelay
            : DateTimeOffset.MinValue;
    }

    public async Task<VersionPageList> PagesAsync(
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        var load = await reader.GetAsync(version, DdragonLanguage.EnUs, Demand, cancellationToken);
        return load.IsReady
            ? VersionPageList.Ready([.. VersionPages.Of(load.Catalog)])
            : VersionPageList.Without(load.Status);
    }

    public async Task<IReadOnlySet<string>> OpenersAsync(
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        var snapshot = await index.GetAsync(cancellationToken);
        return snapshot.OpenedAt(version)
            .Select(static state => state.Path)
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task IngestAsync(PatchVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        await reader.GetAsync(version, DdragonLanguage.EnUs, Waited, cancellationToken);
    }

    // The version the last promotion replaced, the newest ready one below the latest (or any
    // newer one Data Dragon lists): the only one whose rank the caches may still hold wrong.
    private static bool IsLeaving(CatalogVersions versions, PatchVersion version)
    {
        var latest = versions.Latest!;
        var replaced = versions.Ready.FirstOrDefault(ready => ready < latest)
            ?? versions.Listed.FirstOrDefault(listed => listed < latest);
        return replaced is not null && version >= replaced;
    }
}
