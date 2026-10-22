using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Seo.IndexNow;

/// <summary>
/// The index of states as the promotion of a patch reads it, with the patch that was the
/// latest one before: what changed since the previous announcement.
/// </summary>
/// <remarks>
/// IndexNow runs at promotions only. A version listed but never promoted between two
/// promotions is announced by the next one, with the states it closed, so the promotion
/// reads back to the previous latest patch, not to the covered version just before it.
/// </remarks>
/// <param name="States">The index of states, which covers the promoted version.</param>
/// <param name="PreviousLatest">
/// The patch promoted before; null when unknown or before any promotion, the covered version
/// before the promoted one then standing for it.
/// </param>
internal sealed record PromotionIndex(EntityStateSnapshot States, PatchVersion? PreviousLatest)
{
    /// <summary>
    /// The covered versions the promotion of <paramref name="promoted"/> made past ones, newest
    /// first: from the previous latest patch on, before <paramref name="promoted"/>.
    /// </summary>
    public IReadOnlyList<PatchVersion> PastSince(PatchVersion promoted)
    {
        ArgumentNullException.ThrowIfNull(promoted);
        var since = PreviousLatest is { } previous && previous < promoted
            ? previous
            : States.Coverage.Before(promoted);
        return since is null
            ? []
            : [.. States.Coverage.All
                .Where(version => version >= since && version < promoted)
                .Reverse()];
    }
}
