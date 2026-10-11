using System.Text;
using LoDb.Desktop.Tests.Support;
using LoDb.Desktop.Updates.Engine.Signing;
using Microsoft.Extensions.Logging.Testing;
using Velopack;

namespace LoDb.Desktop.Tests.Updates.Signing;

public sealed class SignedFeedsTests : IDisposable
{
    private readonly TestFeedKey _key = new();
    private readonly FakeLogger<SignedFeeds> _logger = new();
    private readonly TestFolder _folder = new();
    private readonly SignedFeeds _feeds;

    public SignedFeedsTests() => _feeds = new SignedFeeds(_key.Verifier(), _logger);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void ReadsTheEntriesOfASignedFeed()
    {
        var assets = _feeds.Accept(_key.Signed(TestReleases.Feed("1.4.0")));

        var asset = Assert.Single(assets);
        Assert.Equal("1.4.0", asset.Version.ToString());
        Assert.Equal(TestReleases.FileName("1.4.0"), asset.FileName);
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public void IgnoresAFeedWithoutSignature()
    {
        var unsigned = _key.Signed(TestReleases.Feed("1.4.0")) with { Signature = null };

        Assert.Empty(_feeds.Accept(unsigned));
        AssertRefused(FeedRefusal.MissingSignature);
    }

    [Fact]
    public void IgnoresAFeedChangedAfterSigning()
    {
        var signed = _key.Signed(TestReleases.Feed("1.4.0"));
        var tampered = signed with { Content = TestReleases.Feed("9.9.9") };

        Assert.Empty(_feeds.Accept(tampered));
        AssertRefused(FeedRefusal.InvalidSignature);
    }

    [Fact]
    public void IgnoresAFeedSignedWithAnotherKey()
    {
        var foreign = new TestFeedKey().Signed(TestReleases.Feed("1.4.0"));

        Assert.Empty(_feeds.Accept(foreign));
        AssertRefused(FeedRefusal.InvalidSignature);
    }

    [Fact]
    public void IgnoresASignedFeedVelopackCannotRead()
    {
        Assert.Empty(_feeds.Accept(_key.Signed(Encoding.UTF8.GetBytes("{\"Assets\": ["))));
        AssertRefused(FeedRefusal.Unreadable);
    }

    [Theory]
    [InlineData("SHA256", null)]
    [InlineData("SHA256", "ABCDEF")]
    [InlineData("Size", 0)]
    [InlineData("FileName", "../LoDb.Desktop-1.4.0-win-x64-full.nupkg")]
    public void IgnoresASignedFeedWithAnIncompleteEntry(string field, object? value)
    {
        var entry = TestReleases.Entry("1.4.0", TestReleases.FileName("1.4.0"));
        entry[field] = value;

        Assert.Empty(_feeds.Accept(_key.Signed(TestReleases.FeedOf([entry]))));
        AssertRefused(FeedRefusal.IncompleteEntry);
    }

    [Fact]
    public async Task AcceptsAPackageThatMatchesItsSignedEntry()
    {
        var asset = Assert.Single(_feeds.Accept(_key.Signed(TestReleases.Feed("1.4.0"))));

        await _feeds.CheckPackageAsync(asset, Write(TestReleases.Package("1.4.0")), Token);
    }

    [Theory]
    [InlineData("package 1.4.1")]
    [InlineData("package 1.4.0 and more")]
    public async Task RefusesAPackageThatDiffersFromItsSignedEntry(string content)
    {
        var asset = Assert.Single(_feeds.Accept(_key.Signed(TestReleases.Feed("1.4.0"))));

        await Assert.ThrowsAsync<UnverifiedPackageException>(() => _feeds.CheckPackageAsync(
            asset,
            Write(Encoding.UTF8.GetBytes(content)),
            Token));
        Assert.Equal("desktop.update.package_refused", _logger.LatestRecord.Id.Name);
    }

    [Fact]
    public async Task RefusesAPackageThatNoSignedFeedLists()
    {
        var unlisted = new VelopackAsset { FileName = TestReleases.FileName("1.4.0") };

        await Assert.ThrowsAsync<UnverifiedPackageException>(() => _feeds.CheckPackageAsync(
            unlisted,
            Write(TestReleases.Package("1.4.0")),
            Token));
    }

    private string Write(byte[] content)
    {
        var path = _folder.Combine("download.partial");
        File.WriteAllBytes(path, content);
        return path;
    }

    private void AssertRefused(FeedRefusal refusal)
    {
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal("desktop.update.feed_refused", record.Id.Name);
        Assert.Equal(refusal.ToString(), record.GetStructuredStateValue("Refusal"));
    }
}
