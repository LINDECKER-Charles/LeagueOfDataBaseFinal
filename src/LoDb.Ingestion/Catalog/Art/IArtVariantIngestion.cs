namespace LoDb.Ingestion.Catalog.Art;

/// <summary>
/// Stores the WebP variants of the latest version's champion art
/// (<see cref="LoDb.Ingestion.Images.Art.ArtVariants"/>), those the manifest does not hold yet.
/// </summary>
/// <remarks>
/// Run by a periodic job of the API rather than with the version's ingestion: the variants
/// only spare the pages the hotlinked art, so a version is promoted without them, and a
/// failure leaves them to the next run. A version promoted meanwhile gets them at the next
/// run; older versions keep the hotlinked art.
/// </remarks>
public interface IArtVariantIngestion
{
    /// <returns>
    /// The variants stored by this run: 0 when none was missing, or no version is ready yet.
    /// </returns>
    Task<int> IngestLatestAsync(CancellationToken cancellationToken);
}
