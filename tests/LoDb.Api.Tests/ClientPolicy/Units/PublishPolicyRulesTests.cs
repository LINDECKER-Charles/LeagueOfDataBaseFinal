using LoDb.Api.Modules.ClientPolicy;
using LoDb.Api.Modules.ClientPolicy.Publishing;

namespace LoDb.Api.Tests.ClientPolicy.Units;

/// <summary>
/// A publication is checked field by field before anything is written: versions of three
/// numbers, a minimum not above the latest, and a complete bundle for Android only.
/// </summary>
public sealed class PublishPolicyRulesTests
{
    public const string Checksum =
        "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    internal static BundleRequest Bundle { get; } = new()
    {
        Id = "web-1.4.2-3f2a9c1",
        Url = "https://github.com/lodb/lodb/releases/download/android-v1.4.2/shell.zip",
        Checksum = Checksum,
        Signature = "c2lnbmF0dXJl",
        MinimumNativeVersion = "1.3.0",
    };

    [Fact]
    public void CompletePolicyHolds()
    {
        var request = new PublishPolicyRequest
        {
            MinimumVersion = "1.2.0",
            LatestVersion = "1.10.0",
            Bundle = Bundle,
        };

        Assert.True(PublishPolicyRules.Check(ClientPlatform.Android, request).IsEmpty);
    }

    [Fact]
    public void EmptyPolicyHolds()
    {
        Assert.True(PublishPolicyRules.Check(ClientPlatform.Desktop, new()).IsEmpty);
    }

    [Theory]
    [InlineData("1.2", null, "minimumVersion", "invalid-version")]
    [InlineData(null, "v1.2.3", "latestVersion", "invalid-version")]
    [InlineData(null, "1.2.3-beta.1", "latestVersion", "invalid-version")]
    [InlineData("2.0.0", "1.9.9", "minimumVersion", "above-latest")]
    public void VersionsAreChecked(string? minimum, string? latest, string field, string code)
    {
        var request = new PublishPolicyRequest { MinimumVersion = minimum, LatestVersion = latest };

        Assert.Equal([(field, code)], ErrorsOf(ClientPlatform.Desktop, request));
    }

    [Fact]
    public void DesktopTakesNoBundle()
    {
        var request = new PublishPolicyRequest { Bundle = Bundle };

        Assert.Equal(
            [("bundle", "bundle-not-supported")],
            ErrorsOf(ClientPlatform.Desktop, request));
    }

    [Fact]
    public void BundleNeedsEveryField()
    {
        var request = new PublishPolicyRequest { Bundle = new BundleRequest() };

        Assert.Equal(
            [
                ("bundle.id", "required"),
                ("bundle.url", "required"),
                ("bundle.checksum", "required"),
                ("bundle.signature", "required"),
                ("bundle.minimumNativeVersion", "required"),
            ],
            ErrorsOf(ClientPlatform.Android, request));
    }

    [Fact]
    public void BundleFieldsAreChecked()
    {
        var request = new PublishPolicyRequest
        {
            Bundle = new BundleRequest
            {
                Id = "web 1.4.2",
                Url = "http://github.com/shell.zip",
                Checksum = Checksum.ToUpperInvariant(),
                Signature = "not base64!",
                MinimumNativeVersion = "1.3",
            },
        };

        Assert.Equal(
            [
                ("bundle.id", "invalid-id"),
                ("bundle.url", "invalid-url"),
                ("bundle.checksum", "invalid-checksum"),
                ("bundle.signature", "invalid-signature"),
                ("bundle.minimumNativeVersion", "invalid-version"),
            ],
            ErrorsOf(ClientPlatform.Android, request));
    }

    private static IEnumerable<(string Field, string Code)> ErrorsOf(
        ClientPlatform platform,
        PublishPolicyRequest request) =>
        PublishPolicyRules.Check(platform, request).ToProblem().Errors!
            .SelectMany(static field => field.Value.Select(code => (field.Key, code)));
}
