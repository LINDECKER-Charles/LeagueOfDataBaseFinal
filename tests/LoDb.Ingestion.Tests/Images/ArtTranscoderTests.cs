using LoDb.Ingestion.Images.Art;
using SkiaSharp;

namespace LoDb.Ingestion.Tests.Images;

/// <summary>
/// An art variant is a WebP of the width asked for, its ratio kept, never wider than the art;
/// a source Skia cannot decode has none.
/// </summary>
public sealed class ArtTranscoderTests
{
    /// <summary>Nothing, a bare JPEG marker, and a JSON document.</summary>
    public static TheoryData<byte[]> Undecodable =>
        [Array.Empty<byte>(), (byte[])[0xFF, 0xD8, 0xFF], "{}"u8.ToArray()];

    [Theory]
    [InlineData(480, 270)]
    [InlineData(960, 540)]
    [InlineData(1280, 720)]
    public void CenteredArtIsScaledToTheWidthAskedFor(int width, int height)
    {
        var art = SyntheticArt.Jpeg(1280, 720);

        var webp = ArtTranscoder.ToWebp(art, width);

        Assert.Equal((width, height), SizeOfWebp(webp));
    }

    [Fact]
    public void NarrowerArtIsNeverEnlarged()
    {
        var portrait = SyntheticArt.Jpeg(ArtVariants.LoadingWidth, 560);

        var webp = ArtTranscoder.ToWebp(portrait, 480);

        Assert.Equal((ArtVariants.LoadingWidth, 560), SizeOfWebp(webp));
    }

    [Theory]
    [MemberData(nameof(Undecodable))]
    public void UndecodableSourceHasNoVariant(byte[] source)
    {
        Assert.Null(ArtTranscoder.ToWebp(source, 480));
    }

    private static (int Width, int Height) SizeOfWebp(byte[]? webp)
    {
        Assert.NotNull(webp);
        Assert.Equal("WEBP"u8.ToArray(), webp[8..12]);
        using var decoded = SKBitmap.Decode(webp);
        return (decoded.Width, decoded.Height);
    }
}
