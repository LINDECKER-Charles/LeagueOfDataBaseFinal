using System.Net;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Api.Tests.Hosting.Support;
using LoDb.Domain.Languages;
using LoDb.Ingestion.Catalog;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Profiles.Units;

/// <summary>
/// The patch a profile is shown on is opened as its caller asks: the server-side renderer,
/// which calls the API directly, has the internal share of the cold loads; a call through
/// nginx, which writes <c>X-Forwarded-For</c>, the public one.
/// </summary>
public sealed class ProfileScopeTests
{
    private const string Version = "7.22.1";
    private const string ForwardedFor = "X-Forwarded-For";
    private const string Visitor = "203.0.113.9";

    // The stack's private network: the renderer's address, and nginx's.
    private static readonly IPAddress StackAddress = IPAddress.Parse("172.18.0.5");

    [Fact]
    public void RendererBrowsesAsInternal()
    {
        var scope = new ProfileQuery(Version, null, StackCall()).Browsing;

        Assert.Equal((Version, DdragonLanguage.EnUs.Code), (scope.Version, scope.Language));
        Assert.Equal(ColdCaller.Internal, scope.Caller);
    }

    [Fact]
    public void CallThroughTheProxyBrowsesAsPublic()
    {
        var context = StackCall();
        context.Request.Headers[ForwardedFor] = Visitor;

        var scope = new ProfileQuery(Version, null, context).Browsing;

        Assert.Equal(ColdCaller.Public, scope.Caller);
    }

    private static DefaultHttpContext StackCall() =>
        StackCalls.From(StackAddress);
}
