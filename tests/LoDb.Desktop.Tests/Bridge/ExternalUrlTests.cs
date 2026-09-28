using LoDb.Desktop.Bridge.Validation;

namespace LoDb.Desktop.Tests.Bridge;

public sealed class ExternalUrlTests
{
    [Theory]
    [InlineData("https://league-of-data-base.com/fr/champions")]
    [InlineData("http://example.com")]
    [InlineData("https://www.leagueoflegends.com/en-us/news/?page=2#top")]
    public void AcceptsRemoteWebAddresses(string value)
    {
        Assert.True(ExternalUrl.TryParse(value, out var url));
        Assert.Equal(new Uri(value), url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/fr/champions")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ms-msdt:/id PCWDiagnostic")]
    [InlineData("ftp://example.com/file")]
    [InlineData("https://user:secret@example.com")]
    [InlineData("https://league-of-data-base.com@evil.example")]
    [InlineData("http://127.0.0.1:5000/desktop/auth/logout")]
    [InlineData("http://localhost/")]
    [InlineData("http://[::1]/")]
    [InlineData("https://example.com/a b")]
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
