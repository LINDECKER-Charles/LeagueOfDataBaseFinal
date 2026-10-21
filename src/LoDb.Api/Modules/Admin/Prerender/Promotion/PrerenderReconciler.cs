using LoDb.Api.Modules.Admin.Prerender.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Promotion;

/// <summary>
/// Heals a promotion whose notice was lost (an instance stopped, a database out of reach): the
/// version the latest one replaced gets the units it lacks, as at its promotion.
/// </summary>
/// <remarks>
/// That version alone, not every older patch without a unit: before the first full pass, they
/// all lack one, and only an operator launches that pass, in the order of the demand.
/// </remarks>
internal sealed class PrerenderReconciler(
    PrerenderActivation activation,
    PromotedVersions promoted,
    VersionEnqueuer enqueuer)
{
    /// <summary>Adds what the last promotion should have added; the units added.</summary>
    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        if (!activation.EnqueuesPromotions
            || await promoted.PreviousOfLatestAsync(cancellationToken) is not { } left)
        {
            return 0;
        }

        var result = await enqueuer.EnsureAsync(left, cancellationToken);
        return result.Units;
    }
}
