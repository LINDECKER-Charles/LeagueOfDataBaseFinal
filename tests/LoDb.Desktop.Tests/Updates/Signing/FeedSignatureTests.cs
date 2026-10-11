using LoDb.Desktop.Updates.Engine;
using LoDb.Desktop.Updates.Engine.Signing;

namespace LoDb.Desktop.Tests.Updates.Signing;

public sealed class FeedSignatureTests
{
    private static readonly byte[] Feed = TestReleases.Feed("1.4.0");
    private static readonly string Fixtures = Path.Combine(
        AppContext.BaseDirectory,
        "Updates",
        "Signing",
        "Fixtures");

    private readonly TestFeedKey _key = new();

    [Fact]
    public void HoldsForTheFeedItsKeySigned()
    {
        Assert.True(_key.Verifier().Holds(TestReleases.FeedName, Feed, _key.Sign(Feed)));
    }

    [Fact]
    public void BreaksWhenASingleByteOfTheFeedChanges()
    {
        var signature = _key.Sign(Feed);
        var tampered = Feed.ToArray();
        tampered[^2] ^= 1;

        Assert.False(_key.Verifier().Holds(TestReleases.FeedName, tampered, signature));
    }

    [Fact]
    public void BreaksWithTheKeyOfAnyoneElse()
    {
        var stranger = new TestFeedKey();

        Assert.False(_key.Verifier().Holds(TestReleases.FeedName, Feed, stranger.Sign(Feed)));
    }

    [Fact]
    public void BreaksForTheFeedOfAnotherChannel()
    {
        var betaSignature = _key.Sign(Feed, "releases.win-x64-beta.json");

        Assert.False(_key.Verifier().Holds(TestReleases.FeedName, Feed, betaSignature));
    }

    [Theory]
    [InlineData("not a signature")]
    [InlineData("AAAA")]
    public void RefusesASignatureItCannotDecode(string signature)
    {
        Assert.False(_key.Verifier().Holds(TestReleases.FeedName, Feed, signature));
    }

    [Fact]
    public void HoldsForAFeedSignedByTheReleaseTool()
    {
        var verifier = new FeedSignature([File.ReadAllText(Fixture("public-key.txt")).Trim()]);

        Assert.True(verifier.Holds(
            TestReleases.FeedName,
            File.ReadAllBytes(Fixture(TestReleases.FeedName)),
            File.ReadAllText(Fixture($"{TestReleases.FeedName}.sig"))));
    }

    // The release that brings a new key is signed with the old one, which installed copies
    // trust; the next ones, with the new key.
    [Fact]
    public void HoldsForEitherKeyWhileAKeyIsRotated()
    {
        var next = new TestFeedKey();
        var rotating = new FeedSignature([_key.PublicKey, next.PublicKey]);

        Assert.True(rotating.Holds(TestReleases.FeedName, Feed, _key.Sign(Feed)));
        Assert.True(rotating.Holds(TestReleases.FeedName, Feed, next.Sign(Feed)));
        Assert.False(rotating.Holds(TestReleases.FeedName, Feed, new TestFeedKey().Sign(Feed)));
    }

    [Fact]
    public void TrustsTheReleaseKeysCompiledIntoTheApp()
    {
        var verifier = new FeedSignature(UpdateFeeds.FeedPublicKeys);

        Assert.Equal(UpdateFeeds.FeedPublicKeys, verifier.PublicKeys);
        Assert.False(verifier.Holds(TestReleases.FeedName, Feed, _key.Sign(Feed)));
    }

    [Fact]
    public void RefusesKeysThatAreNotEd25519PublicKeys()
    {
        var tooShort = Convert.ToBase64String(new byte[31]);

        Assert.Throws<ArgumentException>(() => new FeedSignature([]));
        Assert.Throws<ArgumentException>(() => new FeedSignature([tooShort]));
        Assert.Throws<FormatException>(() => new FeedSignature(["not a key"]));
    }

    private static string Fixture(string name) => Path.Combine(Fixtures, name);
}
