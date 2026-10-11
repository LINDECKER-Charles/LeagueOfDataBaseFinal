using System.Net;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Tests.Hosting.Support;
using LoDb.Ingestion.Catalog;
using Microsoft.AspNetCore.Http;

namespace LoDb.Api.Tests.Catalog;

/// <summary>
/// Who asks for a catalog, read from the connection: the renderer calls from the stack's own
/// network without any forwarding header; whatever comes through nginx, from another private
/// network (one the API shares with other projects) or from a public address, is public.
/// </summary>
public sealed class ColdCallersTests
{
    [Theory]
    [InlineData("172.18.0.5")]
    [InlineData("::ffff:172.18.0.5")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void DirectCallFromTheStackOrTheLoopbackIsInternal(string address) =>
        Assert.Equal(ColdCaller.Internal, ColdCallers.Of(Call(address)));

    [Theory]
    [InlineData("172.20.0.3")]
    [InlineData("10.1.2.3")]
    [InlineData("192.168.1.20")]
    [InlineData("fd00::12")]
    public void AnotherPrivateNetworkIsPublic(string address) =>
        Assert.Equal(ColdCaller.Public, ColdCallers.Of(Call(address)));

    [Theory]
    [InlineData("203.0.113.9")]
    [InlineData("2001:db8::1")]
    [InlineData("172.32.0.1")]
    public void PublicAddressIsPublic(string address) =>
        Assert.Equal(ColdCaller.Public, ColdCallers.Of(Call(address)));

    [Theory]
    [InlineData("X-Forwarded-For")]
    [InlineData("X-Original-For")]
    public void CallThroughTheProxyIsPublicEvenFromTheStack(string header)
    {
        var context = Call("172.18.0.7");
        context.Request.Headers[header] = "172.18.0.1";

        Assert.Equal(ColdCaller.Public, ColdCallers.Of(context));
    }

    [Fact]
    public void ConnectionWithoutAddressIsTheInProcessHost() =>
        Assert.Equal(ColdCaller.Internal, ColdCallers.Of(StackCalls.From(address: null)));

    private static DefaultHttpContext Call(string address) =>
        StackCalls.From(IPAddress.Parse(address));
}
