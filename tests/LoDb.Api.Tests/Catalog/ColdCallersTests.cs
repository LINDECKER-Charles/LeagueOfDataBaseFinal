using System.Net;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Ingestion.Catalog;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// Who asks for a catalog, read from the connection: the renderer calls from the stack's
/// private network without any forwarding header; whatever comes through nginx, or from a
/// public address, is public.
/// </summary>
public sealed class ColdCallersTests
{
    [Theory]
    [InlineData("172.18.0.5")]
    [InlineData("10.1.2.3")]
    [InlineData("192.168.1.20")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:172.18.0.5")]
    [InlineData("fd00::12")]
    public void DirectCallFromThePrivateNetworkIsInternal(string address) =>
        Assert.Equal(ColdCaller.Internal, ColdCallers.Of(Context(address)));

    [Theory]
    [InlineData("203.0.113.9")]
    [InlineData("2001:db8::1")]
    [InlineData("172.32.0.1")]
    public void PublicAddressIsPublic(string address) =>
        Assert.Equal(ColdCaller.Public, ColdCallers.Of(Context(address)));

    [Theory]
    [InlineData("X-Forwarded-For")]
    [InlineData("X-Original-For")]
    public void CallThroughTheProxyIsPublicEvenFromAPrivateAddress(string header)
    {
        var context = Context("172.18.0.7");
        context.Request.Headers[header] = "172.18.0.1";

        Assert.Equal(ColdCaller.Public, ColdCallers.Of(context));
    }

    [Fact]
    public void ConnectionWithoutAddressIsTheInProcessHost() =>
        Assert.Equal(ColdCaller.Internal, ColdCallers.Of(new DefaultHttpContext()));

    private static DefaultHttpContext Context(string address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        return context;
    }
}
