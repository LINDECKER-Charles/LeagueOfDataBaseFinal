using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Revision.Producing;

namespace LoDb.Api.Modules.Admin.Prerender.Promotion;

/// <summary>
/// Heals a promotion whose notice was lost (an instance stopped, a database out of reach, an
/// index of states that covered the patch only later): once the index covers the latest patch,
/// the version it replaced gets the units it lacks, as at its promotion, and the closures of the
/// index the queue of the closed states has not taken yet are enqueued.
/// </summary>
/// <remarks>
/// That version alone, not every older patch without a unit: before the first full pass, they
/// all lack one, and only an operator launches that pass, in the order of the demand. The units
/// only with <c>AutoEnqueueOnPromotion</c>; the closed states whenever the store is configured.
/// While the index lags behind the latest patch, nothing (<see cref="CoveringIndex"/>).
/// </remarks>
internal sealed class PrerenderReconciler(
    PrerenderActivation activation,
    CoveringIndex index,
    PromotedVersions promoted,
    VersionEnqueuer enqueuer,
    RevisionProducer revisions)
{
    /// <summary>Adds what the last promotion should have added; the units added.</summary>
    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        if (!activation.IsActive
            || await promoted.LatestAsync(cancellationToken) is not { } latest
            || await index.OfAsync(latest, cancellationToken) is not { } snapshot)
        {
            return 0;
        }

        var units = 0;
        if (activation.EnqueuesPromotions
            && await promoted.PreviousOfAsync(latest, cancellationToken) is { } left)
        {
            units = (await enqueuer.EnsureAsync(left, cancellationToken)).Units;
        }

        await revisions.ProduceAsync(snapshot, cancellationToken);
        return units;
    }
}
