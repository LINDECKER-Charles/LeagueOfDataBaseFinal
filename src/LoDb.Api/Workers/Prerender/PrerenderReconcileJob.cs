using LoDb.Api.Modules.Admin.Prerender.Promotion;
using LoDb.Infrastructure.Jobs;

namespace LoDb.Api.Workers.Prerender;

/// <summary>
/// The reconciliation of the pre-rendering with the promotions (ADR 0011): every half hour,
/// the patch the latest one replaced gets the units its promotion's notice should have given
/// it, on one instance at a time.
/// </summary>
/// <remarks>
/// The reconciler is resolved per run, from a scope of its own: absent (the module not
/// registered) or dormant (unset, or <c>AutoEnqueueOnPromotion</c> false), a run adds nothing.
/// </remarks>
internal sealed class PrerenderReconcileJob(
    PeriodicJobServices services,
    IServiceScopeFactory scopes) : PeriodicJob(services)
{
    public override string Name => "prerender.reconcile";

    public override TimeSpan Period => TimeSpan.FromMinutes(30);

    protected override async Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var reconciler = scope.ServiceProvider.GetService<PrerenderReconciler>();
        var added = reconciler is null ? 0 : await reconciler.ReconcileAsync(cancellationToken);
        return new(added, "units");
    }
}
