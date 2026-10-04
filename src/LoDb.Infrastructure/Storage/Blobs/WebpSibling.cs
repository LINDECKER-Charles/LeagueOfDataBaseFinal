namespace LoDb.Infrastructure.Storage.Blobs;

/// <summary>
/// The one owner of "which blob has a WebP twin".
/// </summary>
/// <remarks>
/// Only PNG sources are guaranteed to transcode (SVG never does), so the sibling is derived
/// for <c>.png</c> paths only: anything else would point a <c>&lt;picture&gt;</c> at a file
/// that was never written, a silent 404.
/// </remarks>
public static class WebpSibling
{
    /// <summary>Extension of the sibling, without the dot.</summary>
    public const string Extension = "webp";

    /// <summary>Extension of the blobs that have a sibling, without the dot.</summary>
    public const string SourceExtension = "png";

    private const string SourceSuffix = "." + SourceExtension;
    private const string SiblingSuffix = "." + Extension;

    /// <summary>
    /// The sibling of a blob path, in the same form as the input (relative or public), or
    /// <c>null</c> when the blob has none.
    /// </summary>
    public static string? Of(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.EndsWith(SourceSuffix, StringComparison.Ordinal)
            ? string.Concat(path.AsSpan(0, path.Length - SourceSuffix.Length), SiblingSuffix)
            : null;
    }
}
