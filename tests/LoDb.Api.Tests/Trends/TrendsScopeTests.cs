using System.Net;
using LoDb.Api.Modules.Trends;
using LoDb.Domain.Languages;
using LoDb.Ingestion.Catalog;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Trends;

/// <summary>
/// The patch the trends are named on is opened as their caller asks: the server-side renderer,
/// which calls the API directly, has the internal share of the cold loads; a call through
/// nginx, which writes <c>X-Forwarded-For</c>, the public one.
/// </summary>
public sealed class TrendsScopeTests
{
    private const string Version = "7.22.1";
    private const string ForwardedFor = "X-Forwarded-For";
    private const string Visitor = "203.0.113.9";

    // The stack's private network: the renderer's address, and nginx's.
    private static readonly IPAddress StackAddress = IPAddress.Parse("172.18.0.5");

    [Fact]
    public void RendererBrowsesAsInternal()
    {
        var scope = Query(StackCall()).Browsing;

        Assert.Equal((Version, DdragonLanguage.EnUs.Code), (scope.Version, scope.Language));
        Assert.Equal(ColdCaller.Internal, scope.Caller);
    }

    [Fact]
    public void CallThroughTheProxyBrowsesAsPublic()
    {
        var context = StackCall();
        context.Request.Headers[ForwardedFor] = Visitor;

        Assert.Equal(ColdCaller.Public, Query(context).Browsing.Caller);
    }

    private static TrendsQuery Query(HttpContext context) =>
        new(null, null, null, null, Version, null, context);

    private static DefaultHttpContext StackCall() =>
        new() { Connection = { RemoteIpAddress = StackAddress } };
}
