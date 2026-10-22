using LoDb.Api.Modules.Admin.Prerender.Demand.Queue;
using LoDb.Infrastructure.Jobs;

namespace LoDb.Api.Workers.Prerender;

/// <summary>
/// Bounds the automatic queue of the pre-rendering every hour, on one instance
/// (<see cref="DemandPruner"/>): the pages no visitor was refused for a while, then the least
/// requested pending pages past the bound.
/// </summary>
/// <remarks>
/// The pruner is resolved per run, from a scope of its own: absent (the module not
/// registered), a run removes nothing.
/// </remarks>
internal sealed class PrerenderDemandPruneJob(
    PeriodicJobServices services,
    IServiceScopeFactory scopes) : PeriodicJob(services)
{
    public override string Name => "prerender.demand.prune";

    public override TimeSpan Period => TimeSpan.FromHours(1);

    protected override async Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var pruner = scope.ServiceProvider.GetService<DemandPruner>();
        var removed = pruner is null ? 0 : await pruner.PruneAsync(cancellationToken);
        return new(removed, "pages");
    }
}
