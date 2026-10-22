using LoDb.Api.Modules.Admin.Prerender.Demand.Capture;
using LoDb.Api.Modules.Admin.Prerender.Pages;

namespace LoDb.Api.Modules.Admin.Prerender.Demand.Validation;

/// <summary>
/// Keeps of the SSR's refused pages only those a pass would render: a page of an older patch
/// that its catalogue lists, under its canonical path. Junk never enters the queue.
/// </summary>
/// <remarks>
/// A path whose slug is not the canonical one is left out rather than mapped to it: its count
/// is another path's on the SSR, and adding both to one row would read every change of either
/// as a restart. Such a URL answers a redirect anyway.
/// </remarks>
internal sealed class DemandValidator(IPrerenderCatalog catalog, PageIndexCache indexes)
{
    public async Task<JudgedPages> JudgeAsync(
        IReadOnlyList<RefusedPageCount> pages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var pinned = (await catalog.PinnedVersionsAsync(cancellationToken))
            .Select(static version => version.Value)
            .ToHashSet(StringComparer.Ordinal);
        var accepted = new List<CountedPage>();
        var (rejected, deferred) = (0, 0);
        foreach (var refused in pages.DistinctBy(static page => page.Path, StringComparer.Ordinal))
        {
            var (kept, isDeferred) = await JudgeAsync(refused, pinned, cancellationToken);
            if (kept is not null)
            {
                accepted.Add(kept);
            }
            else if (isDeferred)
            {
                deferred++;
            }
            else
            {
                rejected++;
            }
        }

        return new JudgedPages(accepted, rejected, deferred);
    }

    // The page kept, or none: deferred while its version's catalogue is not stored.
    private async Task<(CountedPage? Kept, bool IsDeferred)> JudgeAsync(
        RefusedPageCount refused,
        HashSet<string> pinned,
        CancellationToken cancellationToken)
    {
        var page = DemandPath.Parse(refused.Path);
        if (page is null || !pinned.Contains(page.Version.Value) || refused.Count <= 0)
        {
            return (null, false);
        }

        if (await indexes.OfAsync(page.Version, cancellationToken) is not { } index)
        {
            return (null, true);
        }

        return index.Lists(page.PagePath)
            ? (new CountedPage(page, refused.Count, index), false)
            : (null, false);
    }
}
