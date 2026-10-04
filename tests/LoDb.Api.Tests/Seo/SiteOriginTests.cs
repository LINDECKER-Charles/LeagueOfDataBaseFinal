using LoDb.Api.Modules.Seo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// The origin of the published URLs: the request's own unless a bare canonical origin is
/// configured, anything else refused when the host starts.
/// </summary>
public sealed class SiteOriginTests
{
    private readonly SeoOptionsValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://league-of-data-base.com")]
    [InlineData("https://league-of-data-base.com/")]
    [InlineData("http://localhost:18280")]
    public void AcceptsABareOriginOrNone(string? origin)
    {
        var result = _validator.Validate(null, new SeoOptions { CanonicalOrigin = origin });

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData("league-of-data-base.com")]
    [InlineData("ftp://league-of-data-base.com")]
    [InlineData("https://league-of-data-base.com/en")]
    [InlineData("https://league-of-data-base.com/?a=1")]
    [InlineData("https://league-of-data-base.com/#top")]
    [InlineData("https://user:secret@league-of-data-base.com")]
    public void RefusesAnythingButABareOrigin(string origin)
    {
        var result = _validator.Validate(null, new SeoOptions { CanonicalOrigin = origin });

        Assert.True(result.Failed);
    }

    [Fact]
    public void UsesTheRequestOriginWhenNoneIsConfigured()
    {
        var origins = new SiteOrigin(Options.Create(new SeoOptions()));

        Assert.Equal("https://mirror.example:8443", origins.Of(Request()));
    }

    [Fact]
    public void PrefersTheConfiguredOriginWithoutItsSlash()
    {
        var options = new SeoOptions { CanonicalOrigin = "https://league-of-data-base.com/" };
        var origins = new SiteOrigin(Options.Create(options));

        Assert.Equal("https://league-of-data-base.com", origins.Of(Request()));
    }

    private static HttpRequest Request()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("mirror.example", 8443);
        return context.Request;
    }
}
