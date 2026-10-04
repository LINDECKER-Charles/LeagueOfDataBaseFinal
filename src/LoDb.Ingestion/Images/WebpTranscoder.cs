using SkiaSharp;

namespace LoDb.Ingestion.Images;

/// <summary>
/// Produces the WebP sibling of a stored image, as the legacy <c>ImageTranscoder</c> did.
/// </summary>
/// <remarks>
/// The images keep their native size: the transcode is a pure change of format, alpha
/// included. Skia runs off the request path, in the ingestion's bounded parallelism (A9).
/// </remarks>
internal static class WebpTranscoder
{
    /// <summary>
    /// The legacy quality: visually lossless on the small, flat Data Dragon sprites while
    /// roughly halving their weight.
    /// </summary>
    public const int Quality = 82;

    /// <returns>
    /// The WebP bytes, or <see langword="null"/> when the source is not a raster Skia
    /// decodes: the original is then served alone, as before.
    /// </returns>
    public static byte[]? ToWebp(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty)
        {
            return null;
        }

        using var data = SKData.CreateCopy(source);
        using var image = SKImage.FromEncodedData(data);
        if (image is null)
        {
            return null;
        }

        using var webp = image.Encode(SKEncodedImageFormat.Webp, Quality);
        return webp is null || webp.Size == 0 ? null : webp.ToArray();
    }
}
