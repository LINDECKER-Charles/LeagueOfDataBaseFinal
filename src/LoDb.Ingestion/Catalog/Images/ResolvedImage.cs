using LoDb.Infrastructure.Storage.Blobs;

namespace LoDb.Ingestion.Catalog.Images;

/// <summary>
/// An image as a page shows it: its blob and WebP sibling, or a placeholder.
/// </summary>
public sealed record ResolvedImage
{
    private ResolvedImage(ImageStatus status, string? url, string? webpUrl)
    {
        Status = status;
        Url = url;
        WebpUrl = webpUrl;
    }

    public static ResolvedImage Absent { get; } = new(ImageStatus.Absent, null, null);

    public static ResolvedImage Pending { get; } = new(ImageStatus.Pending, null, null);

    public ImageStatus Status { get; }

    /// <summary>
    /// <c>/cdn/blobs/{sha256}.{ext}</c> when present; <see langword="null"/> for a placeholder.
    /// </summary>
    public string? Url { get; }

    /// <summary>
    /// <c>/cdn/blobs/{sha256}.webp</c>, the sibling every PNG blob is stored with; null for
    /// another format or a placeholder.
    /// </summary>
    public string? WebpUrl { get; }

    public static ResolvedImage Present(BlobKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new ResolvedImage(ImageStatus.Present, key.PublicPath, key.WebpPublicPath);
    }
}
