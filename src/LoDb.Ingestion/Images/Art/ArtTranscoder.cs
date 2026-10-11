using SkiaSharp;

namespace LoDb.Ingestion.Images.Art;

/// <summary>
/// Produces the WebP variant of champion art at a width (<see cref="ArtVariants"/>).
/// </summary>
/// <remarks>
/// The art is scaled down with Mitchell's cubic filter, its ratio kept; an art no wider than
/// the width keeps its own: a variant is never enlarged. Skia runs off the request path, in
/// the bounded parallelism of the job that ingests the variants.
/// </remarks>
internal static class ArtTranscoder
{
    /// <summary>
    /// The art is photographic, unlike the flat icons: 80 is the usual compromise for photos
    /// between the detail kept and the weight.
    /// </summary>
    public const int Quality = 80;

    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    /// <returns>
    /// The WebP bytes, at most <paramref name="width"/> pixels wide, or
    /// <see langword="null"/> when the source is not a raster Skia decodes.
    /// </returns>
    public static byte[]? ToWebp(ReadOnlySpan<byte> source, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        if (source.IsEmpty)
        {
            return null;
        }

        // Decode(span) throws on bytes no codec reads: the codec is asked first.
        using var data = SKData.CreateCopy(source);
        using var codec = SKCodec.Create(data);
        using var decoded = codec is null ? null : SKBitmap.Decode(codec);
        if (decoded is null)
        {
            return null;
        }

        if (decoded.Width <= width)
        {
            return Encode(decoded);
        }

        using var resized = decoded.Resize(SizeAt(decoded, width), Sampling);
        return resized is null ? null : Encode(resized);
    }

    private static SKSizeI SizeAt(SKBitmap art, int width)
    {
        var height = Math.Round(
            (double)art.Height * width / art.Width,
            MidpointRounding.AwayFromZero);
        return new SKSizeI(width, Math.Max((int)height, 1));
    }

    private static byte[]? Encode(SKBitmap art)
    {
        using var webp = art.Encode(SKEncodedImageFormat.Webp, Quality);
        return webp is null || webp.Size == 0 ? null : webp.ToArray();
    }
}
