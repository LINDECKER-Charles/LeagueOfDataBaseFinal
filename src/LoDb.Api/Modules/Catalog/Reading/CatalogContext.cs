using LoDb.Api.Modules.Catalog.Http;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Catalog.Snapshots;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>
/// A loaded catalog, with the images and the caching of the answers built from it.
/// </summary>
internal sealed class CatalogContext(
    CatalogSnapshot catalog,
    PatchVersion? latest,
    IImageResolver resolver)
{
    public CatalogSnapshot Catalog => catalog;

    /// <summary>
    /// One manifest query for the images of an answer; entries without an image are skipped.
    /// </summary>
    /// <param name="images">The images the answer shows.</param>
    /// <param name="demand">
    /// Synchronous for a detail, the search and the pickers; queued for a list.
    /// </param>
    /// <param name="cancellationToken">Aborts the wait, never a shared ingestion.</param>
    public async Task<ImageSet> ResolveAsync(
        IEnumerable<DdragonImage?> images,
        ColdDemand demand,
        CancellationToken cancellationToken)
    {
        List<DdragonImage> wanted = [.. images.OfType<DdragonImage>()];
        if (wanted.Count == 0)
        {
            return ImageSet.Empty;
        }

        var resolution = await resolver
            .ResolveAsync(catalog.Version, wanted, demand, cancellationToken);
        return new ImageSet(resolution);
    }

    /// <summary>
    /// The answer, cached as its version allows, or briefly with a <c>Retry-After</c> while
    /// one of <paramref name="images"/> is still a pending placeholder.
    /// </summary>
    public CachedJson<TValue> Answer<TValue>(TValue value, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(images);
        var cache = images.HasPending
            ? CacheHeaders.Pending
            : CacheHeaders.Of(catalog.Version, latest);
        return new CachedJson<TValue>(value, cache);
    }
}
