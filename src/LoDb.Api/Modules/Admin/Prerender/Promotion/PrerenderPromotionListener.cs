using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Revision.Producing;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Pipeline.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Promotion;

/// <summary>
/// At the promotion of a patch, once the index of states covers it: gives the one it replaced,
/// now an older patch, its units, some 24,000 pages that the site serves from the store once
/// written, its patch page among them; then enqueues the pages of the states the promotion
/// closed, to render them again (<see cref="RevisionProducer"/>).
/// </summary>
/// <remarks>
/// <para>
/// An index that does not cover the patch yet (its step of the promotion failed) still holds
/// open the states the promotion closed: nothing starts on it (<see cref="CoveringIndex"/>).
/// </para>
/// <para>
/// The units only with <see cref="PrerenderOptions.AutoEnqueueOnPromotion"/> (production; the
/// staging stays almost empty); the closed states whenever the store is configured, staging
/// included. Told once, on the instance that promoted, without a retry: the reconciliation of
/// the worker (<see cref="PrerenderReconciler"/>) heals a notice lost or an index late.
/// </para>
/// </remarks>
internal sealed class PrerenderPromotionListener(
    PrerenderActivation activation,
    CoveringIndex index,
    PromotedVersions promoted,
    VersionEnqueuer enqueuer,
    RevisionProducer revisions) : IPromotionListener
{
    public PromotionStage Stage => PromotionStage.Prerender;

    public async Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (!activation.IsActive
            || await index.OfAsync(version, cancellationToken) is not { } snapshot)
        {
            return;
        }

        if (activation.EnqueuesPromotions
            && await promoted.PreviousOfAsync(version, cancellationToken) is { } left)
        {
            await enqueuer.EnsureAsync(left, cancellationToken);
        }

        await revisions.ProduceAsync(snapshot, cancellationToken);
    }
}
