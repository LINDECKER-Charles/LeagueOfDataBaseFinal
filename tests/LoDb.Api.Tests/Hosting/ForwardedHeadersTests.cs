using System.Net;
using LoDb.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Hosting;

/// <summary>
/// The client address is taken from <c>X-Forwarded-For</c> only when the request comes from
/// the configured proxy network, loopback when none is configured.
/// </summary>
public sealed class ForwardedHeadersTests
{
    [Fact]
    public async Task ConfiguredNetworkReplacesTheLoopbackDefaults()
    {
        await using var factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?>
            {
                ["LoDb:Hosting:KnownProxyNetworks:0"] = "172.30.0.0/16",
            },
        };

        var options = Read(factory);

        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            options.ForwardedHeaders);
        var network = Assert.Single(options.KnownIPNetworks);
        Assert.Equal(System.Net.IPNetwork.Parse("172.30.0.0/16"), network);
        Assert.Empty(options.KnownProxies);
    }

    [Fact]
    public async Task WithoutConfigurationOnlyLoopbackIsTrusted()
    {
        await using var factory = new ApiFactory();

        var options = Read(factory);

        Assert.All(options.KnownIPNetworks, static network => Assert.True(
            network.Contains(IPAddress.Loopback) || network.Contains(IPAddress.IPv6Loopback)));
        Assert.All(options.KnownProxies, static proxy => Assert.True(IPAddress.IsLoopback(proxy)));
        Assert.True(options.KnownIPNetworks.Count + options.KnownProxies.Count > 0);
    }

    private static ForwardedHeadersOptions Read(ApiFactory factory) =>
        factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
}
