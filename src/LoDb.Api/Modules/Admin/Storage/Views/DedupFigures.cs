namespace LoDb.Api.Modules.Admin.Storage.Views;

/// <summary>
/// What the content addressing saves: the image references of the asset index against the
/// blobs actually stored.
/// </summary>
internal sealed record DedupFigures
{
    /// <summary>Images the asset index points to, one per version and key.</summary>
    public required long LogicalRefs { get; init; }

    /// <summary>Blobs stored, WebP siblings left out.</summary>
    public required long PhysicalBlobs { get; init; }

    /// <summary>References per blob; 0 without blobs.</summary>
    public required double Ratio { get; init; }

    /// <summary>The references beyond the blobs, at the mean size of a blob.</summary>
    public required long SavedBytesApprox { get; init; }
}
