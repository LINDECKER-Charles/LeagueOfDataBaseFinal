using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Snapshots;

/// <summary>
/// What tells two reads of the index apart: the current generation and how far it
/// covers. A rebuild changes the first, a promotion that extends the index the second.
/// </summary>
/// <param name="GenerationId">The current generation's row.</param>
/// <param name="CoverageEnd">Its newest covered version; null before the first one.</param>
public sealed record StateIndexStamp(long GenerationId, PatchVersion? CoverageEnd);
