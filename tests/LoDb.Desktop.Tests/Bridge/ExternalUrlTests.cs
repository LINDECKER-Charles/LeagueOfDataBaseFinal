using LoDb.Desktop.Bridge.Validation;

namespace LoDb.Desktop.Tests.Bridge;

public sealed class ExternalUrlTests
{
    [Theory]
    [InlineData("https://league-of-data-base.com/fr/champions")]
    [InlineData("https://www.leagueoflegends.com/en-us/news/?page=2#top")]
    public void AcceptsRemoteHttpsAddresses(string value)
    {
        Assert.True(ExternalUrl.TryParse(value, out var url));
        Assert.Equal(new Uri(value), url);
    }

    // One row per check: absent, a blank or control character, not absolute, a scheme other
    // than https (cleartext and protocol handlers alike), credentials, a loopback host.
    [Theory]
    [InlineData(null)]
    [InlineData(" https://example.com")]
    [InlineData("/fr/champions")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://league-of-data-base.com@evil.example")]
    [InlineData("https://localhost/")]
    public void RefusesAnythingElse(string? value)
    {
        Assert.False(ExternalUrl.TryParse(value, out var url));
        Assert.Null(url);
    }

    [Fact]
    public void RefusesUrlsLongerThanTheLimit()
    {
        var value = "https://example.com/" + new string('a', ExternalUrl.MaxLength);

        Assert.False(ExternalUrl.TryParse(value, out _));
    }
}
