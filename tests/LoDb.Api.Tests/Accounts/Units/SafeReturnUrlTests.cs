using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// The page a sign-in returns to stays on the site: a local path as it is, a URL of the
/// origin the request came to reduced to its path, anything else dropped.
/// </summary>
public sealed class SafeReturnUrlTests
{
    private const string Host = "leagueofdatabase.com";

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/en/items?tier=legendary", "/en/items?tier=legendary")]
    [InlineData("https://leagueofdatabase.com/en/runes?page=2#top", "/en/runes?page=2")]
    [InlineData("HTTPS://LeagueOfDatabase.COM/fr", "/fr")]
    public void PageOfTheSiteIsKept(string returnUrl, string expected) =>
        Assert.Equal(expected, SafeReturnUrl.Resolve(returnUrl, Request()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("//evil.example/phish")]
    [InlineData("/\\evil.example/phish")]
    [InlineData("/en/\nphish")]
    [InlineData("https://evil.example/phish")]
    [InlineData("https://leagueofdatabase.com.evil.example/")]
    [InlineData("https://leagueofdatabase.com@evil.example/")]
    [InlineData("http://leagueofdatabase.com/en")]
    [InlineData("https://leagueofdatabase.com:8443/en")]
    [InlineData("javascript:alert(1)")]
    [InlineData("en/items")]
    public void AnythingElseIsDropped(string? returnUrl) =>
        Assert.Null(SafeReturnUrl.Resolve(returnUrl, Request()));

    private static HttpRequest Request()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = Uri.UriSchemeHttps;
        context.Request.Host = new HostString(Host);
        return context.Request;
    }
}
