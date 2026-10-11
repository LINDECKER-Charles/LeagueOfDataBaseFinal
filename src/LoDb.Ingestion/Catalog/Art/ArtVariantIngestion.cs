using LoDb.Domain.Languages;
using LoDb.Ingestion.Catalog.Reading;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Images.Art;
using Microsoft.Extensions.Options;

namespace LoDb.Ingestion.Catalog.Art;

/// <inheritdoc cref="IArtVariantIngestion"/>
/// <remarks>
/// The champions are those of the latest en_US catalog, which the instance holds pinned in
/// memory: a run that finds nothing missing costs one manifest query. Fetches and
/// transcodes run <c>LoDb:Catalog:ArtVariantsConcurrency</c> at a time, so that the run
/// never takes the API's processor from the pages.
/// </remarks>
internal sealed class ArtVariantIngestion(
    ICatalogReader reader,
    ImageIngestion images,
    IOptions<CatalogOptions> options) : IArtVariantIngestion
{
    private static readonly ColdDemand Demand = ColdDemand.StoredOnly.By(ColdCaller.Internal);

    public async Task<int> IngestLatestAsync(CancellationToken cancellationToken)
    {
        if (await reader.GetLatestAsync(cancellationToken).ConfigureAwait(false) is not { } latest)
        {
            return 0;
        }

        var load = await reader
            .GetAsync(latest, DdragonLanguage.EnUs, Demand, cancellationToken)
            .ConfigureAwait(false);
        if (load.Catalog is not { } catalog)
        {
            return 0;
        }

        var batch = new ImageBatch(
            latest,
            [.. catalog.Champions.Entries.SelectMany(ArtVariants.Of)],
            Force: false)
        {
            MaxParallelism = options.Value.ArtVariantsConcurrency,
        };
        var report = await images.IngestAsync(batch, cancellationToken).ConfigureAwait(false);
        return report.Stored;
    }
}
