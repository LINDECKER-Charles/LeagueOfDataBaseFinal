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

    [Theory]
    [InlineData(null)]
    [InlineData("/fr/champions")]
    [InlineData("http://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ms-msdt:/id PCWDiagnostic")]
    [InlineData("https://league-of-data-base.com@evil.example")]
    [InlineData("https://localhost/")]
    [InlineData(" https://example.com")]
    [InlineData("https://example.com/\n")]
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
