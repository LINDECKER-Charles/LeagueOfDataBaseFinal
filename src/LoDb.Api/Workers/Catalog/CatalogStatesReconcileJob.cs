using LoDb.Infrastructure.Jobs;
using LoDb.Ingestion.Catalog.States.Updates;

namespace LoDb.Api.Workers.Catalog;

/// <summary>
/// The reconciliation of the index of states with the promotions (ADR 0003): every ten
/// minutes, on one instance at a time, the index extends to the latest promoted version, the
/// en_US datasets of the versions it lacks ingested first.
/// </summary>
/// <remarks>
/// It repairs a promotion whose first stage failed or found the lock taken, and a promotion
/// made by the <c>ingest</c> command elsewhere; it builds the index when none is current.
/// Nothing guarantees a run at start-up: the shared schedule may say the last run is recent.
/// </remarks>
internal sealed class CatalogStatesReconcileJob(
    PeriodicJobServices services,
    IStateIndexUpdater updater) : PeriodicJob(services)
{
    private const string Unit = "versions";

    private static readonly TimeSpan Every = TimeSpan.FromMinutes(10);

    public override string Name => "catalog.states.reconcile";

    public override TimeSpan Period => Every;

    protected override async Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken)
    {
        var request = new StateIndexRequest { Trigger = StateIndexTrigger.Reconcile };
        var report = await updater.UpdateAsync(request, cancellationToken);
        return new JobRunSummary(report.VersionsIndexed, Unit);
    }
}
