using LoDb.Api.Cli.ClientPolicy;
using LoDb.Api.Modules.ClientPolicy;

namespace LoDb.Api.Tests.ClientPolicy.Units;

/// <summary>
/// The options of <c>client-policy publish</c>: the whole policy of one app, host settings
/// let through, and a misspelt option refused rather than read as a cleared field.
/// </summary>
public sealed class ClientPolicyPublishArgumentsTests
{
    [Fact]
    public void OptionsMakeTheWholePolicy()
    {
        var options = ClientPolicyPublishArguments.Parse(
        [
            "--platform", "android",
            "--minimum=1.2.0",
            "--latest", "1.4.2",
            "--bundle-id", "web-1.4.2",
            "--bundle-url", "https://example.test/shell.zip",
            "--bundle-checksum", PublishPolicyRulesTests.Checksum,
            "--bundle-signature", "c2ln",
            "--bundle-minimum-native", "1.3.0",
            "--ConnectionStrings:LoDb=Host=db",
        ]);

        Assert.Equal(ClientPlatform.Android, options.Platform);
        Assert.Equal(
            ("1.2.0", "1.4.2"),
            (options.Request.MinimumVersion, options.Request.LatestVersion));
        var bundle = options.Request.Bundle;
        Assert.NotNull(bundle);
        Assert.Equal(
            ("web-1.4.2", "https://example.test/shell.zip", "c2ln", "1.3.0"),
            (bundle.Id, bundle.Url, bundle.Signature, bundle.MinimumNativeVersion));
        Assert.Equal(PublishPolicyRulesTests.Checksum, bundle.Checksum);
    }

    [Fact]
    public void LeftOutOptionsAreCleared()
    {
        var options = ClientPolicyPublishArguments.Parse(["--platform=desktop"]);

        Assert.Equal(ClientPlatform.Desktop, options.Platform);
        Assert.Equal(new(), options.Request);
    }

    [Fact]
    public void OneBundleOptionMakesABundle()
    {
        var options = ClientPolicyPublishArguments.Parse(
            ["--platform", "android", "--bundle-id", "web-1"]);

        Assert.Equal("web-1", options.Request.Bundle?.Id);
        Assert.Null(options.Request.Bundle?.Url);
    }

    // Arguments separated by one space; none for the empty line.
    [Theory]
    [InlineData("")]
    [InlineData("--platform ios")]
    [InlineData("--platform")]
    [InlineData("--platform desktop --minimun 1.0.0")]
    [InlineData("--platform desktop --minimun=1.0.0")]
    [InlineData("--platform desktop --latest 1.0.0 --latest 1.1.0")]
    [InlineData("--platform desktop 1.0.0")]
    [InlineData("--platform desktop --minimum --latest 1.0.0")]
    public void InvalidUseIsRefused(string line) =>
        Assert.Throws<FormatException>(() => ClientPolicyPublishArguments.Parse(
            line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
}
