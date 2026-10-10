using System.Net;
using LoDb.Api.Modules.Builds.Sharing;
using LoDb.Api.Tests.Hosting.Support;
using LoDb.Ingestion.Catalog;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Builds;

/// <summary>
/// The patch of a shared build is opened as its caller asks: the server-side renderer, which
/// calls the API directly, has the internal share of the cold loads; a call through nginx,
/// which writes <c>X-Forwarded-For</c>, the public one.
/// </summary>
public sealed class ShareScopeTests
{
    private const string Version = "7.22.1";
    private const string BuildLanguage = "fr_FR";
    private const string ForwardedFor = "X-Forwarded-For";
    private const string Visitor = "203.0.113.9";

    // The stack's private network: the renderer's address, and nginx's.
    private static readonly IPAddress StackAddress = IPAddress.Parse("172.18.0.5");

    [Fact]
    public void RendererOpensTheBuildsPatchAsInternal()
    {
        var query = new ShareQuery(null, null, StackCall());

        var scope = query.ScopeOf(Version, BuildLanguage);

        Assert.Equal((Version, BuildLanguage), (scope.Version, scope.Language));
        Assert.Equal(ColdCaller.Internal, scope.Caller);
    }

    [Fact]
    public void CallThroughTheProxyOpensItAsPublic()
    {
        var context = StackCall();
        context.Request.Headers[ForwardedFor] = Visitor;

        var scope = new ShareQuery(null, null, context).ScopeOf(Version, BuildLanguage);

        Assert.Equal(ColdCaller.Public, scope.Caller);
    }

    private static DefaultHttpContext StackCall() =>
        StackCalls.From(StackAddress);
}
