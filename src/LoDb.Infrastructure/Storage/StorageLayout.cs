namespace LoDb.Infrastructure.Storage;

/// <summary>
/// Layout of the storage root (<c>LoDb:Storage:Root</c>, <c>/srv/storage</c> in a container).
/// </summary>
/// <remarks>
/// Only <see cref="BlobsDirectory"/> is web-facing: nginx maps <see cref="PublicBlobsPrefix"/>
/// onto it and answers 404 for everything else (ADR 0004).
/// </remarks>
public static class StorageLayout
{
    /// <summary>Content-addressed images: <c>blobs/{sha256}.{ext}</c>.</summary>
    public const string BlobsDirectory = "blobs";

    /// <summary>Immutable datasets: <c>data/{version}/{lang}/{type}.json</c>.</summary>
    public const string DataDirectory = "data";

    /// <summary>
    /// Hidden staging area of the atomic writes. It lives inside the root so that the final
    /// rename never crosses a volume.
    /// </summary>
    public const string StagingDirectory = ".staging";

    /// <summary>Public URL prefix of the blobs, served straight from disk by nginx.</summary>
    public const string PublicBlobsPrefix = "/cdn/blobs/";

    /// <summary>File extension of the datasets.</summary>
    public const string DatasetExtension = ".json";
}
