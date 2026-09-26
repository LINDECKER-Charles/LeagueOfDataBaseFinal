using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>The resolved images of one answer.</summary>
internal sealed class ImageSet
{
    private readonly ImageResolution? resolution;

    public ImageSet(ImageResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        this.resolution = resolution;
    }

    private ImageSet() => resolution = null;

    /// <summary>An answer that shows no image.</summary>
    public static ImageSet Empty { get; } = new();

    /// <summary>Whether a placeholder stands for an image a later read may find.</summary>
    public bool HasPending => resolution?.HasPending ?? false;

    /// <summary>
    /// How an image resolved; an entry without a usable image shows the absent placeholder.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The image was not resolved with this set.</exception>
    public CatalogImage Of(DdragonImage? image) =>
        image is null || resolution is null
            ? CatalogImage.Absent
            : CatalogImage.From(resolution[image]);
}
