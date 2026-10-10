namespace LoDb.Api.Tests.Catalog.Metrics;

/// <summary>
/// The catalog's metrics, scraped from a host of their own once no other host of the test run
/// starts or stops.
/// </summary>
/// <remarks>
/// A host's metrics provider listens to the meters of that name in the whole process, the
/// other test hosts' included: a series first published by another host leaves the scrape
/// when that host stops. In production a process runs one host.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CatalogMetricsGroup : ICollectionFixture<CatalogApiFixture>
{
    public const string Name = "Catalog metrics";
}
