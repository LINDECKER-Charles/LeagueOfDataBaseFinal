using System.Net;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Api.Tests.Hosting.Support;
using LoDb.Domain.Languages;
using LoDb.Ingestion.Catalog;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Profiles.Units;

/// <summary>
/// The patch a profile is shown on is opened as its caller asks: the server-side renderer,
/// which calls the API directly, has the internal share of the cold loads. A call through
/// nginx is public by <c>ColdCallers</c>, which the profile query reads.
/// </summary>
public sealed class ProfileScopeTests
{
    private const string Version = "7.22.1";

    // The stack's private network: the renderer's address, and nginx's.
    private static readonly IPAddress StackAddress = IPAddress.Parse("172.18.0.5");

    [Fact]
    public void RendererBrowsesAsInternal()
    {
        var scope = new ProfileQuery(Version, null, StackCall()).Browsing;

        Assert.Equal((Version, DdragonLanguage.EnUs.Code), (scope.Version, scope.Language));
        Assert.Equal(ColdCaller.Internal, scope.Caller);
    }

    private static DefaultHttpContext StackCall() =>
        StackCalls.From(StackAddress);
}
