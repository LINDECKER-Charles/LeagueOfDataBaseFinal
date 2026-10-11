using System.Text;
using LoDb.Ingestion.Images;

namespace LoDb.Ingestion.Tests.Images;

/// <summary>
/// A blob is stored only under an extension nginx serves (<c>/cdn/blobs/</c>), and only when
/// its first bytes are those of the format that extension names.
/// </summary>
public sealed class BlobMediaTypesTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00];
    private static readonly byte[] Gif87 = Bytes("GIF87a....");
    private static readonly byte[] Gif89 = Bytes("GIF89a....");
    private static readonly byte[] Webp = Bytes("RIFF\0\0\0\0WEBPVP8 ");
    private static readonly byte[] Webm = [0x1A, 0x45, 0xDF, 0xA3, 0x01];
    private static readonly byte[] Mp4 = Bytes("\0\0\0\u0018ftypisom");
    private static readonly byte[] Svg = Bytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
    private static readonly byte[] Html = Bytes("<!doctype html><script>1</script>");

    /// <summary>Each allowed extension with bytes of its own format.</summary>
    public static TheoryData<string, byte[]> Accepted => new()
    {
        { "png", Png },
        { "jpg", Jpeg },
        { "jpeg", Jpeg },
        { "gif", Gif87 },
        { "gif", Gif89 },
        { "webp", Webp },
        { "webm", Webm },
        { "mp4", Mp4 },
    };

    /// <summary>Another format under an allowed name, or a name nginx does not serve.</summary>
    public static TheoryData<string, byte[]> Refused => new()
    {
        { "png", Jpeg },
        { "png", Svg },
        { "png", Html },
        { "png", [] },
        { "png", [0x89, 0x50] },
        { "jpg", Png },
        { "gif", Webp },
        { "webp", Bytes("RIFF\0\0\0\0WAVEfmt ") },
        { "mp4", Webm },
        { "webm", Mp4 },
        { "svg", Svg },
        { "html", Html },
        { "js", Bytes("alert(1)") },
        { "dds", Bytes("DDS |...") },
        { "PNG", Png },
    };

    [Theory]
    [MemberData(nameof(Accepted))]
    public void BytesOfTheFormatTheExtensionNamesAreAccepted(string extension, byte[] content) =>
        Assert.True(BlobMediaTypes.Matches(extension, content));

    [Theory]
    [MemberData(nameof(Refused))]
    public void AnyOtherFileIsRefused(string extension, byte[] content) =>
        Assert.False(BlobMediaTypes.Matches(extension, content));

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
