using LoDb.Api.Tests.Ingestion;
using LoDb.Api.Workers.Catalog;
using LoDb.Infrastructure.Jobs;
using LoDb.Testing;
using LoDb.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// The job storing the art variants: resolved from the zones and run once per period on one
/// instance; with no version ready yet, it stores nothing.
/// </summary>
public sealed class ArtVariantsJobTests(PostgresContainerFixture postgres)
{
    private static CancellationToken Token => IngestionHost.Token;

    [Fact]
    public async Task JobRunsOncePerPeriod()
    {
        await using var host = await IngestionHost.CreateAsync(postgres);
        await using var services = host.BuildServices(DdragonFixtures.CreateReplay());
        using var job = ActivatorUtilities.CreateInstance<ArtVariantsJob>(services);

        var first = await job.RunIfDueAsync(Token);
        var second = await job.RunIfDueAsync(Token);

        Assert.Equal(("catalog.art-variants", TimeSpan.FromMinutes(30)), (job.Name, job.Period));
        Assert.Equal((JobRunOutcome.Succeeded, JobRunOutcome.NotDue), (first, second));
    }
}
