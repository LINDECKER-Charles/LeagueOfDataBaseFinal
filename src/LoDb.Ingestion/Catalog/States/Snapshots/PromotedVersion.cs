using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Snapshots;

/// <summary>
/// A version that became the latest one, and when (<c>ddragon_version.promoted_at</c>): what
/// dates the patch page of the version before it.
/// </summary>
/// <param name="Version">The promoted version.</param>
/// <param name="PromotedAt">When it was promoted.</param>
public sealed record PromotedVersion(PatchVersion Version, DateTimeOffset PromotedAt);
