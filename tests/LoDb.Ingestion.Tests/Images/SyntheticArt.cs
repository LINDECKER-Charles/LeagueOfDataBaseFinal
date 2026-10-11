using SkiaSharp;

namespace LoDb.Ingestion.Tests.Images;

/// <summary>
/// Champion art of Data Dragon's sizes, drawn rather than recorded: the recording keeps the
/// images the versions ingest, and the art is hotlinked but for its WebP variants.
/// </summary>
internal static class SyntheticArt
{
    private const int JpegQuality = 90;

    /// <summary>A JPEG of the size given, a gradient that a scaled copy keeps visible.</summary>
    public static byte[] Jpeg(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                [SKColors.Gold, SKColors.MidnightBlue],
                SKShaderTileMode.Clamp),
        };
        surface.Canvas.DrawRect(0, 0, width, height, paint);
        using var image = surface.Snapshot();
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return jpeg.ToArray();
    }
}
