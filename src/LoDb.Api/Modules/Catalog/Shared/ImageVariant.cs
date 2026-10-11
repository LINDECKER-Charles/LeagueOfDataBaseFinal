namespace LoDb.Api.Modules.Catalog.Shared;

/// <summary>A WebP variant of an art, stored by the site: one candidate of a srcset.</summary>
internal sealed record ImageVariant
{
    /// <summary><c>/cdn/blobs/{sha256}.webp</c>.</summary>
    public required string Url { get; init; }

    /// <summary>Its width in pixels, the <c>w</c> descriptor of the candidate.</summary>
    public required int Width { get; init; }
}
