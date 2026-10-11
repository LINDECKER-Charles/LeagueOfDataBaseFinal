using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Ingestion.Catalog.Images;
using LoDb.Ingestion.Images;

namespace LoDb.Api.Modules.Catalog.Reading;

/// <summary>The resolved images of one answer, and the art variants it carries.</summary>
internal sealed class ImageSet
{
    private readonly ImageResolution? resolution;
    private readonly ImageResolution? art;

    public ImageSet(ImageResolution resolution)
        : this(resolution, null) => ArgumentNullException.ThrowIfNull(resolution);

    private ImageSet(ImageResolution? resolution, ImageResolution? art)
    {
        this.resolution = resolution;
        this.art = art;
    }

    /// <summary>An answer that shows no image.</summary>
    public static ImageSet Empty { get; } = new(null, null);

    /// <summary>
    /// Whether a placeholder stands for an image a later read may find; an art variant the
    /// store lacks does not count: the answer is whole without it.
    /// </summary>
    public bool HasPending => resolution?.HasPending ?? false;

    /// <summary>
    /// How an image resolved; an entry without a usable image shows the absent placeholder.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The image was not resolved with this set.</exception>
    public CatalogImage Of(DdragonImage? image) =>
        image is null || resolution is null
            ? CatalogImage.Absent
            : CatalogImage.From(resolution[image]);

    /// <summary>The same images, with the art variants resolved apart.</summary>
    public ImageSet WithArt(ImageResolution variants)
    {
        ArgumentNullException.ThrowIfNull(variants);
        return new ImageSet(resolution, variants);
    }

    /// <summary>
    /// The variants of an art the store holds, in the order given; none until they are
    /// stored, the client showing the hotlinked art meanwhile.
    /// </summary>
    /// <exception cref="KeyNotFoundException">A variant was not resolved with this set.</exception>
    public IReadOnlyList<ImageVariant> VariantsOf(IEnumerable<DdragonImage> variants)
    {
        ArgumentNullException.ThrowIfNull(variants);
        if (art is null)
        {
            return [];
        }

        List<ImageVariant> stored = [];
        foreach (var variant in variants)
        {
            if (variant.Width is { } width && art[variant].Url is { } url)
            {
                stored.Add(new ImageVariant { Url = url, Width = width });
            }
        }

        return stored;
    }
}
