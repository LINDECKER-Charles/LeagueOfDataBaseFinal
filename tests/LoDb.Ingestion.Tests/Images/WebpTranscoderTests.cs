using LoDb.Ingestion.Images;
using LoDb.Testing.Fixtures;
using SkiaSharp;

namespace LoDb.Ingestion.Tests.Images;

/// <summary>
/// The WebP sibling keeps the size of its source; a source Skia cannot decode has none.
/// </summary>
public sealed class WebpTranscoderTests
{
    private static readonly Uri AhriPortrait =
        new("https://ddragon.leagueoflegends.com/cdn/16.19.1/img/champion/Ahri.png");

    /// <summary>Nothing, a bare PNG signature, and a JSON document.</summary>
    public static TheoryData<byte[]> Undecodable =>
        [Array.Empty<byte>(), (byte[])[0x89, 0x50, 0x4E, 0x47], "{}"u8.ToArray()];

    [Fact]
    public void PngBecomesAWebpOfTheSameSize()
    {
        var png = File.ReadAllBytes(DdragonFixtures.BodyPath(AhriPortrait));

        var webp = WebpTranscoder.ToWebp(png);

        Assert.NotNull(webp);
        Assert.Equal("RIFF"u8.ToArray(), webp[..4]);
        Assert.Equal("WEBP"u8.ToArray(), webp[8..12]);
        using var source = SKBitmap.Decode(png);
        using var result = SKBitmap.Decode(webp);
        Assert.NotNull(result);
        Assert.Equal((source.Width, source.Height), (result.Width, result.Height));
    }

    [Theory]
    [MemberData(nameof(Undecodable))]
    public void UndecodableSourceHasNoWebp(byte[] source)
    {
        var webp = WebpTranscoder.ToWebp(source);

        Assert.Null(webp);
    }
}
