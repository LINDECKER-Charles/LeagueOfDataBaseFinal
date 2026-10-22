using LoDb.Api.Modules.Admin.Prerender.Promotion;
using LoDb.Infrastructure.Jobs;

namespace LoDb.Api.Workers.Prerender;

/// <summary>
/// The reconciliation of the pre-rendering with the promotions (ADR 0011): every half hour,
/// once the index of states covers the latest patch, the patch it replaced gets the units its
/// promotion's notice should have given it, and the states the index closed since the queue of
/// the closed states last took them are enqueued, on one instance at a time.
/// </summary>
/// <remarks>
/// The reconciler is resolved per run, from a scope of its own: absent (the module not
/// registered) or dormant (unset), a run adds nothing; <c>AutoEnqueueOnPromotion</c> false, no
/// unit, the closed states still. Its summary counts the units; the queue says what it took in
/// a line of its own.
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
