using LoDb.Ingestion.Catalog.Images;

namespace LoDb.Api.Modules.Catalog.Shared;

/// <summary>
/// An image as a client renders it: its blob and WebP sibling, or a placeholder.
/// </summary>
/// <remarks>
/// A <c>pending</c> placeholder may resolve on a later read, which the answer's
/// <c>Retry-After</c> invites once; an <c>absent</c> one never will.
/// </remarks>
internal sealed record CatalogImage
{
    public static CatalogImage Absent { get; } = new() { Status = ImageStatus.Absent };

    public required ImageStatus Status { get; init; }

    /// <summary><c>/cdn/blobs/{sha256}.{ext}</c>; null for a placeholder.</summary>
    public string? Url { get; init; }

    /// <summary><c>/cdn/blobs/{sha256}.webp</c>; null for a placeholder or a non-PNG.</summary>
    public string? WebpUrl { get; init; }

    public static CatalogImage From(ResolvedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new CatalogImage { Status = image.Status, Url = image.Url, WebpUrl = image.WebpUrl };
    }
}
