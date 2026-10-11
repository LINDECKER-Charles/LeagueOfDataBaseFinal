using LoDb.Infrastructure.Jobs;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Art;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Workers.Catalog;

/// <summary>
/// Stores the WebP variants of the latest version's champion art missing from the manifest,
/// every <c>LoDb:Catalog:ArtVariantsPeriod</c> on one instance at a time (ADR 0003): at
/// startup, then after each promotion within a period.
/// </summary>
internal sealed class ArtVariantsJob(
    PeriodicJobServices services,
    IArtVariantIngestion ingestion,
    IOptions<CatalogOptions> options) : PeriodicJob(services)
{
    public override string Name => "catalog.art-variants";

    public override TimeSpan Period => options.Value.ArtVariantsPeriod;

    protected override async Task<JobRunSummary> RunOnceAsync(
        CancellationToken cancellationToken) =>
        new(await ingestion.IngestLatestAsync(cancellationToken), "variants");
}
