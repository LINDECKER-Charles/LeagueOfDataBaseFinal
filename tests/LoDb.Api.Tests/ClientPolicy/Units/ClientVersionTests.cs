using LoDb.Api.Modules.ClientPolicy;
using LoDb.Api.Modules.ClientPolicy.Versions;

namespace LoDb.Api.Tests.ClientPolicy.Units;

/// <summary>
/// The version of an app against the minimum of its policy: numbers compared as numbers, a
/// beta below its release, and a header the API cannot read treated as the web's.
/// </summary>
public sealed class ClientVersionTests
{
    [Theory]
    [InlineData("1.4.0", "1.4.0", false)]
    [InlineData("1.4.1", "1.4.0", false)]
    [InlineData("1.10.0", "1.9.0", false)]
    [InlineData("2.0.0", "1.99.99", false)]
    [InlineData("1.3.9", "1.4.0", true)]
    [InlineData("1.9.0", "1.10.0", true)]
    [InlineData("0.99.0", "1.0.0", true)]
    [InlineData("1.4.0-beta.3", "1.4.0", true)]
    [InlineData("1.4.1-beta.1", "1.4.0", false)]
    [InlineData("1.3.0-rc-2", "1.4.0", true)]
    public void VersionIsComparedWithTheMinimum(string client, string minimum, bool isBelow)
    {
        Assert.True(ClientVersion.TryParse(client, out var version));
        Assert.True(AppVersion.TryParse(minimum, out var floor));

        Assert.Equal(isBelow, version.IsBelow(floor));
        Assert.Equal(client, version.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.4")]
    [InlineData("1.4.0.2")]
    [InlineData("v1.4.0")]
    [InlineData(" 1.4.0")]
    [InlineData("1.4.0-")]
    [InlineData("1.4.0-beta..1")]
    [InlineData("1.4.0-beta_1")]
    [InlineData("1.4.0+build.5")]
    [InlineData("1.-4.0")]
    [InlineData("99999999999.0.0")]
    public void UnreadableVersionsAreRefused(string? text)
    {
        Assert.False(ClientVersion.TryParse(text, out _));
    }

    [Fact]
    public void OverlongVersionIsRefused()
    {
        Assert.False(ClientVersion.TryParse("1.0.0-" + new string('a', 64), out _));
    }

    [Theory]
    [InlineData("desktop/1.4.0", "Desktop", "1.4.0")]
    [InlineData("android/2.0.1-beta.2", "Android", "2.0.1-beta.2")]
    public void HeaderNamesTheAppAndItsVersion(
        string header,
        string platform,
        string version)
    {
        var client = ClientHeader.Parse(header);

        Assert.NotNull(client);
        Assert.Equal(
            (Enum.Parse<ClientPlatform>(platform), version),
            (client.Platform, client.Version.ToString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("desktop")]
    [InlineData("web/1.0.0")]
    [InlineData("ios/1.0.0")]
    [InlineData("Desktop/1.0.0")]
    [InlineData("desktop/")]
    [InlineData("desktop/1.0")]
    [InlineData("desktop/1.0.0/extra")]
    public void HeaderThatNamesNoKnownAppIsIgnored(string? header)
    {
        Assert.Null(ClientHeader.Parse(header));
    }
}
