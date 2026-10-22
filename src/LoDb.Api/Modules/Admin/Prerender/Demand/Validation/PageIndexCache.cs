using System.Collections.Concurrent;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.Reading;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Validation;

/// <summary>
/// The page lists of the older patches and their openers, kept an hour each: a crawl refuses
/// pages of a few hundred versions, whose catalogue the queue would otherwise read at each of
/// its reads. A version's openers no longer change once the index covers it.
/// </summary>
/// <remarks>
/// Bounded: past <see cref="MostVersions"/> lists, it forgets them all. A version whose
/// catalogue is not stored has no list: <see cref="IPrerenderCatalog.PagesAsync"/> queues its
/// ingestion, and its pages are judged again at their next refusal.
/// </remarks>
internal sealed class PageIndexCache(IPrerenderCatalog catalog, TimeProvider clock)
{
    /// <summary>Over the older patches Data Dragon lists (some 170 in 2026).</summary>
    public const int MostVersions = 512;

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, (VersionPageIndex Index, DateTimeOffset At)>
        _indexes = new(StringComparer.Ordinal);

    /// <summary>
    /// The pages of <paramref name="version"/>; null while its catalogue is not stored.
    /// </summary>
    public async Task<VersionPageIndex?> OfAsync(
        PatchVersion version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        var now = clock.GetUtcNow();
        if (_indexes.TryGetValue(version.Value, out var known) && now - known.At < Lifetime)
        {
            return known.Index;
        }

        var list = await catalog.PagesAsync(version, cancellationToken);
        if (list.Status != CatalogLoadStatus.Ready)
        {
            return null;
        }

        if (_indexes.Count >= MostVersions)
        {
            _indexes.Clear();
        }

        var openers = await catalog.OpenersAsync(version, cancellationToken);
        var index = new VersionPageIndex(list.Paths, openers);
        _indexes[version.Value] = (index, now);
        return index;
    }
}
