using LoDb.Api.Tests.Cli;
using LoDb.Api.Tests.Ingestion;
using LoDb.Api.Workers.Catalog;
using LoDb.Infrastructure.Jobs;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Workers;

/// <summary>
/// The reconciliation of the index of states: a periodic job resolved from the zones, which
/// builds the index when the API started on none.
/// </summary>
public sealed class CatalogStatesReconcileJobTests(PostgresContainerFixture postgres)
{
    private const string CoverageEnd =
        "SELECT last_version FROM catalog_state_generation WHERE is_current";

    [Fact]
    public async Task ReconciliationBuildsTheIndexWhenNoneIsCurrent()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        await PromotedEnglishCatalog.StoreAsync(host);
        await using var services = host.BuildServices(DdragonFixtures.CreateReplay());
        using var job = ActivatorUtilities.CreateInstance<CatalogStatesReconcileJob>(services);

        var outcome = await job.RunIfDueAsync(IngestionHost.Token);

        Assert.Equal(
            ("catalog.states.reconcile", TimeSpan.FromMinutes(10)),
            (job.Name, job.Period));
        Assert.Equal(JobRunOutcome.Succeeded, outcome);
        Assert.Equal([DdragonFixtures.Latest.Value], await host.QueryAsync(CoverageEnd));
    }
}
