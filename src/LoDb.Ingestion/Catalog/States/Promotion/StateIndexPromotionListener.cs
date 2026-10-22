using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Updates;
using LoDb.Ingestion.Pipeline.Versions;

namespace LoDb.Ingestion.Catalog.States.Promotion;

/// <summary>
/// The first stage of a promotion: the index of states extends to the promoted version,
/// listed version by listed version, before the pre-rendering and the announcements read what
/// the promotion changed from it.
/// </summary>
/// <remarks>
/// A failure (an intermediate en_US dataset Data Dragon does not give) is the promotion's
/// listener failure, logged without undoing it: the reconciliation extends the index later,
/// and the later stages, finding the version uncovered, log and stop. A lock held elsewhere is
/// no failure: its holder extends the index.
/// </remarks>
internal sealed class StateIndexPromotionListener(IStateIndexUpdater updater) : IPromotionListener
{
    public PromotionStage Stage => PromotionStage.StateIndex;

    public async Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        var request = new StateIndexRequest { Trigger = StateIndexTrigger.Promotion };
        await updater.UpdateAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
