using LoDb.Ingestion.Images;

namespace LoDb.Ingestion.Catalog.Images;

/// <summary>The images of one resolution, by manifest key.</summary>
public sealed class ImageResolution
{
    private readonly IReadOnlyDictionary<(string Type, string File), ResolvedImage> images;

    internal ImageResolution(
        IReadOnlyDictionary<(string Type, string File), ResolvedImage> images,
        bool refused)
    {
        this.images = images;
        Refused = refused;
    }

    /// <summary>Whether an image is left pending: the answer deserves a retry.</summary>
    public bool HasPending =>
        images.Values.Any(static image => image.Status == ImageStatus.Pending);

    /// <summary>
    /// Whether the queue refused the pending images (queue full, crawler budget spent): they
    /// come with no later read but a new one.
    /// </summary>
    public bool Refused { get; }

    /// <summary>
    /// How <paramref name="image"/> resolved; images sharing its manifest key resolve alike.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The image was not part of the resolution.</exception>
    public ResolvedImage this[DdragonImage image]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(image);
            return images.TryGetValue((image.ManifestType, image.File), out var resolved)
                ? resolved
                : throw new KeyNotFoundException($"{image.File} was not resolved.");
        }
    }
}
