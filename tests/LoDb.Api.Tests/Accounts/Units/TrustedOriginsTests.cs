using LoDb.Api.Modules.Accounts;
using LoDb.Api.Modules.Accounts.Protection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Accounts.Units;

/// <summary>
/// The pages trusted with unsafe requests: those of the origin the request came to, of the
/// configured public origin, and of the Android app; a request without <c>Origin</c> comes
/// from no page at all.
/// </summary>
public sealed class TrustedOriginsTests
{
    private const string SiteOrigin = "https://leagueofdatabase.com";
    private const string InternalHost = "api:8080";

    private static readonly TrustedOrigins Origins = new(Options.Create(new AccountsOptions
    {
        SiteOrigin = SiteOrigin,
    }));

    [Theory]
    [InlineData("https://leagueofdatabase.com", "leagueofdatabase.com")]
    [InlineData("https://LeagueOfDatabase.com", "leagueofdatabase.com")]
    [InlineData("https://leagueofdatabase.com:8443", "leagueofdatabase.com")]
    [InlineData("https://leagueofdatabase.com", InternalHost)]
    [InlineData("https://localhost", InternalHost)]
    [InlineData("https://beta.leagueofdatabase.com", "beta.leagueofdatabase.com")]
    public void OwnConfiguredAndAppOriginsAreTrusted(string origin, string host) =>
        Assert.True(Origins.Allows(Request(host, origin)));

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://leagueofdatabase.com")]
    [InlineData("https://leagueofdatabase.com.evil.example")]
    [InlineData("https://beta.leagueofdatabase.com")]
    [InlineData("http://localhost")]
    [InlineData("https://leagueofdatabase.com/en")]
    [InlineData("null")]
    [InlineData("")]
    public void AnyOtherOriginIsRefused(string origin) =>
        Assert.False(Origins.Allows(Request(InternalHost, origin)));

    [Fact]
    public void PortCountsWhenTheHostCarriesOne()
    {
        Assert.True(Origins.Allows(Request("localhost:4200", "https://localhost:4200")));
        Assert.False(Origins.Allows(Request("localhost:4200", "https://localhost:4300")));
    }

    [Fact]
    public void RequestWithoutOriginIsNoPageOfAnotherSite() =>
        Assert.True(Origins.Allows(Request(InternalHost, origin: null)));

    [Fact]
    public void SeveralOriginsAreRefused()
    {
        var request = Request(InternalHost, origin: null);
        request.Headers.Origin = new([SiteOrigin, SiteOrigin]);

        Assert.False(Origins.Allows(request));
    }

    private static HttpRequest Request(string host, string? origin)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = Uri.UriSchemeHttps;
        context.Request.Host = new HostString(host);
        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        return context.Request;
    }
}
