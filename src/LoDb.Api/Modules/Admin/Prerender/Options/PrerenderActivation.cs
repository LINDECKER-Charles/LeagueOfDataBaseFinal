using LoDb.Infrastructure.PageStore;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Options;

/// <summary>
/// Whether the pre-rendering may run here: the pass and its store are both configured.
/// </summary>
/// <remarks>
/// Until then, the worker, the promotion and its reconciliation do nothing, and say nothing:
/// a local stack without a store and a host whose writer key is not set yet are the normal
/// case, not a fault.
/// </remarks>
internal sealed class PrerenderActivation(
    IOptions<PrerenderOptions> pass,
    IOptions<PageStoreOptions> store)
{
    public bool IsActive => pass.Value.IsConfigured && store.Value.IsConfigured;

    /// <summary>
    /// Whether a patch that leaves the latest rank joins the pass by itself
    /// (<see cref="PrerenderOptions.AutoEnqueueOnPromotion"/>), the pass being active.
    /// </summary>
    public bool EnqueuesPromotions => IsActive && pass.Value.AutoEnqueueOnPromotion;
}
