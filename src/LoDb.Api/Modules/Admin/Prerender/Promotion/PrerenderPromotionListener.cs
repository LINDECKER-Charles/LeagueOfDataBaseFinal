using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Pipeline.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Promotion;

/// <summary>
/// At the promotion of a patch, gives the one it replaced, now an older patch, its units:
/// some 24,000 pages that the site serves from the store once written.
/// </summary>
/// <remarks>
/// Only with <see cref="PrerenderOptions.AutoEnqueueOnPromotion"/> (production; the staging
/// stays almost empty). Told once, on the instance that promoted, without a retry: the
/// reconciliation of the worker (<see cref="PrerenderReconciler"/>) heals a notice lost.
/// </remarks>
internal sealed class PrerenderPromotionListener(
    PrerenderActivation activation,
    PromotedVersions promoted,
    VersionEnqueuer enqueuer) : IPromotionListener
{
    public async Task OnPromotedAsync(PatchVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (!activation.EnqueuesPromotions)
        {
            return;
        }

        if (await promoted.PreviousOfAsync(version, cancellationToken) is { } left)
        {
            await enqueuer.EnsureAsync(left, cancellationToken);
        }
    }
}
