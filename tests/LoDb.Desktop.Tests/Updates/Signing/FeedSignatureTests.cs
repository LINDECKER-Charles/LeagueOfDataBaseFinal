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
    [InlineData("")]
    [InlineData("not a signature")]
    [InlineData("AAAA")]
    public void RefusesASignatureItCannotDecode(string signature)
    {
        Assert.False(_key.Verifier().Holds(TestReleases.FeedName, Feed, signature));
    }

    [Fact]
    public void HoldsForAFeedSignedByTheReleaseTool()
    {
        var verifier = new FeedSignature(File.ReadAllText(Fixture("public-key.txt")).Trim());

        Assert.True(verifier.Holds(
            TestReleases.FeedName,
            File.ReadAllBytes(Fixture(TestReleases.FeedName)),
            File.ReadAllText(Fixture($"{TestReleases.FeedName}.sig"))));
    }

    [Fact]
    public void TrustsTheReleaseKeyCompiledIntoTheApp()
    {
        var verifier = new FeedSignature(UpdateFeeds.FeedPublicKey);

        Assert.Equal(UpdateFeeds.FeedPublicKey, verifier.PublicKey);
        Assert.False(verifier.Holds(TestReleases.FeedName, Feed, _key.Sign(Feed)));
    }

    [Fact]
    public void RefusesAKeyThatIsNotAnEd25519PublicKey()
    {
        var tooShort = Convert.ToBase64String(new byte[31]);

        Assert.Throws<ArgumentException>(() => new FeedSignature(tooShort));
        Assert.Throws<FormatException>(() => new FeedSignature("not a key"));
    }

    private static string Fixture(string name) => Path.Combine(Fixtures, name);
}
