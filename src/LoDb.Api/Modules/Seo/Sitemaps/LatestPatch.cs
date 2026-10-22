using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Seo.Sitemaps;

/// <summary>The latest version as the sitemaps announce and date it.</summary>
/// <param name="Version">The latest version; null before the first promotion.</param>
/// <param name="PromotedAt">
/// Its promotion: the date of every page it changed, and of every locale's
/// <c>latest.xml</c>.
/// </param>
/// <param name="HasChanges">
/// Whether it changed something, so that its page of changes is indexable.
/// </param>
internal sealed record LatestPatch(
    PatchVersion? Version,
    DateTimeOffset? PromotedAt,
    bool HasChanges)
{
    /// <summary>The latest version, what it changed read in <paramref name="index"/>.</summary>
    public static LatestPatch Of(
        PatchVersion? version,
        DateTimeOffset? promotedAt,
        EntityStateSnapshot index)
    {
        ArgumentNullException.ThrowIfNull(index);
        return new(version, promotedAt, version is not null && index.HasChanges(version));
    }
}
