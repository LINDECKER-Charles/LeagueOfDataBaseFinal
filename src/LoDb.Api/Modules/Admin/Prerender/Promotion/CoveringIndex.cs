using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Admin.Prerender.Promotion;

/// <summary>
/// The index of states as the pre-rendering's share of a promotion reads it: only once it covers
/// the promoted patch. Before, it still holds open the states that promotion closed, and the
/// pages rendered on it would be stored as revisable for good.
/// </summary>
/// <remarks>
/// Neither the units of the patch the promotion replaced nor the queue of the closed states
/// start on an index that lags behind: the reconciliation of the pre-rendering gives both once
/// the index caught up (its own reconciliation extends it).
/// </remarks>
internal sealed class CoveringIndex(IEntityStateIndex index, ILogger<CoveringIndex> logger)
{
    private const string NoCoverage = "none";

    /// <summary>
    /// The index, when it covers <paramref name="promoted"/>; null, said in a line, when it
    /// does not yet.
    /// </summary>
    /// <exception cref="StateIndexUnavailableException">The index cannot be read.</exception>
    public async Task<EntityStateSnapshot?> OfAsync(
        PatchVersion promoted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(promoted);
        var snapshot = await index.GetAsync(cancellationToken);
        if (snapshot.Coverage.Contains(promoted))
        {
            return snapshot;
        }

        var end = snapshot.Coverage.End?.Value ?? NoCoverage;
        PrerenderLog.IndexBehind(logger, promoted.Value, end);
        return null;
    }
}
