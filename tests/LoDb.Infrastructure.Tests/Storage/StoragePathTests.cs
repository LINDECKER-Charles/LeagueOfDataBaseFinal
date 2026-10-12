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

    // One row per class the segment pattern refuses: empty, a first character that is no
    // letter or digit (traversal, absolute path), a separator inside, a trailing line break
    // that a `$` would let through, and over-long.
    public static TheoryData<string> InvalidSegments =>
    [
        "",
        "..",
        "/etc",
        "14/1",
        @"a\b",
        "en_US\n",
        new string('a', 101),
    ];

    [Theory]
    [MemberData(nameof(InvalidSegments))]
    public void AnInvalidVersionIsRejected(string version) =>
        Assert.Throws<ArgumentException>(() => new DatasetKey(version, "en_US", "champion"));

    // The language goes through the version's segment check: one row proves it is checked.
    [Fact]
    public void AnInvalidLanguageIsRejected() =>
        Assert.Throws<ArgumentException>(() => new DatasetKey("14.1.1", "14/1", "champion"));

    // Each segment of a type goes through the version's check: these rows cover what a
    // multi-segment type adds, an empty segment and a traversal between two valid ones.
    [Theory]
    [InlineData("/champion")]
    [InlineData("championDetail/../champion")]
    public void AnInvalidTypeIsRejected(string type) =>
        Assert.Throws<ArgumentException>(() => new DatasetKey("14.1.1", "en_US", type));

    [Theory]
    [InlineData("14.1.1", "en_US", "champion", "data/14.1.1/en_US/champion.json")]
    [InlineData("7.22.1", "ar_AE", "championDetail/MonkeyKing",
        "data/7.22.1/ar_AE/championDetail/MonkeyKing.json")]
    public void AValidKeyMapsToItsDataPath(
        string version,
        string language,
        string type,
        string expected) =>
        Assert.Equal(expected, new DatasetKey(version, language, type).RelativePath);

    // Uppercase, short, and a trailing line break.
    [Theory]
    [InlineData("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")]
    [InlineData("ba7816bf")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad\n")]
    public void AnInvalidShaIsRejected(string sha256) =>
        Assert.Throws<ArgumentException>(() => new BlobKey(sha256, "png"));

    // Empty, a character outside lowercase letters and digits, a trailing line break, and
    // over-long.
    [Theory]
    [InlineData("")]
    [InlineData("PNG")]
    [InlineData("png\n")]
    [InlineData("averylongext")]
    public void AnInvalidBlobExtensionIsRejected(string extension) =>
        Assert.Throws<ArgumentException>(() => new BlobKey(ValidSha256, extension));
}
