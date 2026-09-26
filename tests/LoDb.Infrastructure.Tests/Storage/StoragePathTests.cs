using LoDb.Infrastructure.Storage.Blobs;
using LoDb.Infrastructure.Storage.Datasets;

namespace LoDb.Infrastructure.Tests.Storage;

/// <summary>
/// Caller-supplied path parts never climb out of their directory nor name an absolute path.
/// </summary>
public sealed class StoragePathTests
{
    private const string ValidSha256 =
        "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    public static TheoryData<string> InvalidSegments =>
    [
        "",
        ".",
        "..",
        ".staging",
        "../14.1.1",
        "/etc",
        "en_US\n",
        @"a\b",
        @"C:\data",
        "C:",
        "~",
        "en US",
        "-rf",
        "é",
        new string('a', 101),
    ];

    public static TheoryData<string> InvalidTypes =>
    [
        "",
        "/champion",
        "champion/",
        "a//b",
        "../../../etc/passwd",
        "championDetail/../champion",
        "championDetail/..",
        "./champion",
        @"..\champion",
        "/etc/passwd",
    ];

    [Theory]
    [MemberData(nameof(InvalidSegments))]
    public void AnInvalidVersionIsRejected(string version) =>
        Assert.Throws<ArgumentException>(() => new DatasetKey(version, "en_US", "champion"));

    [Theory]
    [MemberData(nameof(InvalidSegments))]
    public void AnInvalidLanguageIsRejected(string language) =>
        Assert.Throws<ArgumentException>(() => new DatasetKey("14.1.1", language, "champion"));

    [Theory]
    [InlineData("14/1")]
    [InlineData("14.1.1/..")]
    public void AVersionOrLanguageIsASingleSegment(string segment)
    {
        Assert.Throws<ArgumentException>(() => new DatasetKey(segment, "en_US", "champion"));
        Assert.Throws<ArgumentException>(() => new DatasetKey("14.1.1", segment, "champion"));
    }

    [Theory]
    [MemberData(nameof(InvalidSegments))]
    [MemberData(nameof(InvalidTypes))]
    public void AnInvalidTypeIsRejected(string type) =>
        Assert.Throws<ArgumentException>(() => new DatasetKey("14.1.1", "en_US", type));

    [Theory]
    [InlineData("14.1.1", "en_US", "champion", "data/14.1.1/en_US/champion.json")]
    [InlineData("0.151.2", "zh_CN", "item", "data/0.151.2/zh_CN/item.json")]
    [InlineData("7.22.1", "ar_AE", "championDetail/MonkeyKing",
        "data/7.22.1/ar_AE/championDetail/MonkeyKing.json")]
    [InlineData("14.1.1", "cdragon", "chromas/266", "data/14.1.1/cdragon/chromas/266.json")]
    public void AValidKeyMapsToItsDataPath(
        string version,
        string language,
        string type,
        string expected) =>
        Assert.Equal(expected, new DatasetKey(version, language, type).RelativePath);

    [Theory]
    [InlineData("")]
    [InlineData("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")]
    [InlineData("ba7816bf")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad\n")]
    [InlineData("../../../../../../../../../../../../../../../../../../../../../../etc")]
    public void AnInvalidShaIsRejected(string sha256) =>
        Assert.Throws<ArgumentException>(() => new BlobKey(sha256, "png"));

    [Theory]
    [InlineData("")]
    [InlineData("PNG")]
    [InlineData(".png")]
    [InlineData("p/g")]
    [InlineData("png\n")]
    [InlineData("..")]
    public void AnInvalidBlobExtensionIsRejected(string extension) =>
        Assert.Throws<ArgumentException>(() => new BlobKey(ValidSha256, extension));

    [Fact]
    public void KeysWithTheSamePartsAreEqual()
    {
        Assert.Equal(new BlobKey(ValidSha256, "png"), new BlobKey(ValidSha256, "png"));
        Assert.Equal(
            new DatasetKey("14.1.1", "en_US", "champion"),
            new DatasetKey("14.1.1", "en_US", "champion"));
    }
}
