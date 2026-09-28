using LoDb.Api.Modules.PublicApi;
using LoDb.Api.Modules.PublicApi.Keys.Reference;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.PublicApiKeys;

/// <summary>
/// The base URL <c>/developers</c> documents: the configured one, else the site's origin;
/// and the check that refuses a malformed one at startup.
/// </summary>
public sealed class ReferenceTests
{
    private const string Site = "https://league-of-data-base.com/";

    [Theory]
    [InlineData(null, "https://league-of-data-base.com")]
    [InlineData("  ", "https://league-of-data-base.com")]
    [InlineData("https://api.league-of-data-base.com/", "https://api.league-of-data-base.com")]
    [InlineData("http://localhost:18080", "http://localhost:18080")]
    public void TheConfiguredBaseUrlWinsOverTheSite(string? configured, string documented)
    {
        var endpoint = new ReferenceEndpoint(
            Options.Create(new PublicApiOptions { SiteOrigin = Site }),
            Options.Create(new ReferenceOptions { BaseUrl = configured }));

        Assert.Equal(documented, endpoint.Read().Value!.BaseUrl);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("https://api.league-of-data-base.com", true)]
    [InlineData("https://api.league-of-data-base.com/v1", false)]
    [InlineData("ftp://api.league-of-data-base.com", false)]
    [InlineData("api.league-of-data-base.com", false)]
    public void OnlyABareWebOriginIsAccepted(string? configured, bool accepted)
    {
        var result = new ReferenceOptionsValidator()
            .Validate(Options.DefaultName, new ReferenceOptions { BaseUrl = configured });

        Assert.Equal(accepted, result.Succeeded);
    }
}
