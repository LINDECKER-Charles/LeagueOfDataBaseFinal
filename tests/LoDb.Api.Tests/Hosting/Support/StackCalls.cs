using System.Net;
using LoDb.Api.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Hosting.Support;

/// <summary>
/// Requests as the API sees them on a served host whose stack network is <see cref="Subnet"/>,
/// for the code that tells the stack's own calls from the others (<see cref="StackNetwork"/>).
/// </summary>
internal static class StackCalls
{
    /// <summary>The stack's private network: the renderer's address, and nginx's.</summary>
    public const string Subnet = "172.18.0.0/16";

    /// <summary>A direct call from <paramref name="address"/>; none for an in-process host.</summary>
    public static DefaultHttpContext From(IPAddress? address) => new()
    {
        Connection = { RemoteIpAddress = address },
        RequestServices = new StackServices(new StackNetwork(Options.Create(new HostingOptions
        {
            KnownProxyNetworks = [Subnet],
        }))),
    };

    private sealed class StackServices(StackNetwork network) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(StackNetwork) ? network : null;
    }
}
