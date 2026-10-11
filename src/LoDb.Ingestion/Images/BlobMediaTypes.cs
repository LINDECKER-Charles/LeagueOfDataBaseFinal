using LoDb.Infrastructure.Storage.Blobs;

namespace LoDb.Ingestion.Images;

/// <summary>
/// The media a blob may be: the extensions nginx serves under <c>/cdn/blobs/</c>
/// (<c>docker/nginx/sites/site.conf</c>), each with the leading bytes its format starts with.
/// </summary>
/// <remarks>
/// A blob is served from the site's own origin, by its extension. One whose extension is not
/// listed, or whose bytes are not of the format its extension names, is never stored: an
/// upstream file served under an image name could otherwise reach the site as markup or
/// script.
/// </remarks>
internal static class BlobMediaTypes
{
    private const int RiffTagOffset = 8;
    private const int FtypOffset = 4;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif87 = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89 = "GIF89a"u8.ToArray();
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();
    private static readonly byte[] Ebml = [0x1A, 0x45, 0xDF, 0xA3];
    private static readonly byte[] Ftyp = "ftyp"u8.ToArray();

    private static readonly Dictionary<string, Func<ReadOnlyMemory<byte>, bool>> Formats =
        new(StringComparer.Ordinal)
        {
            [WebpSibling.SourceExtension] = static bytes => StartsWith(bytes, 0, Png),
            ["jpg"] = static bytes => StartsWith(bytes, 0, Jpeg),
            ["jpeg"] = static bytes => StartsWith(bytes, 0, Jpeg),
            ["gif"] = static bytes => StartsWith(bytes, 0, Gif87) || StartsWith(bytes, 0, Gif89),
            [WebpSibling.Extension] = static bytes =>
                StartsWith(bytes, 0, Riff) && StartsWith(bytes, RiffTagOffset, Webp),
            ["webm"] = static bytes => StartsWith(bytes, 0, Ebml),
            ["mp4"] = static bytes => StartsWith(bytes, FtypOffset, Ftyp),
        };

    /// <summary>
    /// Whether <paramref name="content"/> starts as the format of
    /// <paramref name="extension"/>; false for an extension that is not allowed.
    /// </summary>
    public static bool Matches(string extension, ReadOnlyMemory<byte> content) =>
        Formats.TryGetValue(extension, out var matches) && matches(content);

    private static bool StartsWith(ReadOnlyMemory<byte> content, int offset, byte[] signature) =>
        content.Length >= offset + signature.Length
        && content.Span.Slice(offset, signature.Length).SequenceEqual(signature);
}
