namespace LoDb.Api.Modules.Admin.Storage.Views;

/// <summary>The content-addressed images and the WebP siblings of the PNG ones.</summary>
internal sealed record BlobFigures
{
    /// <summary>Blobs by extension, the heaviest first.</summary>
    public required IReadOnlyList<StorageRow> ByExt { get; init; }

    /// <summary>PNG blobs, the ones that get a WebP sibling.</summary>
    public required long Sources { get; init; }

    /// <summary>PNG blobs whose WebP sibling is written.</summary>
    public required long WebpSiblings { get; init; }

    /// <summary>Share of the sources that have their sibling, from 0 to 1.</summary>
    public required double WebpCoverage { get; init; }

    public required long SourceBytes { get; init; }

    public required long WebpBytes { get; init; }
}
