using System.Globalization;
using System.Net;
using System.Net.Sockets;
using LoDb.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace LoDb.Api.Tests.Hosting.Support;

/// <summary>
/// The API on a real Kestrel, on free ports, as a served host runs it: the in-memory server
/// applies none of Kestrel's limits, and Development turns a bad request into an exception.
/// </summary>
public sealed class KestrelApi : IAsyncDisposable
{
    private const string HttpPortKey = "LoDb:Hosting:HttpPort";
    private const string MetricsPortKey = "LoDb:Hosting:MetricsPort";

    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _host;

    private KestrelApi(ApiFactory factory, WebApplicationFactory<Program> host, int httpPort)
    {
        _factory = factory;
        _host = host;
        BaseAddress = new Uri($"http://{IPAddress.Loopback}:{httpPort}");
    }

    /// <summary>The API port, on the loopback.</summary>
    public Uri BaseAddress { get; }

    /// <param name="settings">Settings of the host, besides its two ports.</param>
    public static KestrelApi Start(IReadOnlyDictionary<string, string?> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var (httpPort, metricsPort) = FreePorts();
        var factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?>(settings)
            {
                [HttpPortKey] = httpPort.ToString(CultureInfo.InvariantCulture),
                [MetricsPortKey] = metricsPort.ToString(CultureInfo.InvariantCulture),
            },
        };
        var host = factory.WithWebHostBuilder(
            static builder => builder.UseEnvironment(Environments.Production));
        host.UseKestrel();
        host.StartServer();
        return new KestrelApi(factory, host, httpPort);
    }

    public HttpClient CreateClient() => new() { BaseAddress = BaseAddress };

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        await _factory.DisposeAsync();
    }

    // Both listeners stay open until both ports are known, so they cannot be the same one.
    private static (int Http, int Metrics) FreePorts()
    {
        var http = new TcpListener(IPAddress.Loopback, 0);
        var metrics = new TcpListener(IPAddress.Loopback, 0);
        http.Start();
        metrics.Start();
        try
        {
            return (PortOf(http), PortOf(metrics));
        }
        finally
        {
            http.Stop();
            metrics.Stop();
        }
    }

    private static int PortOf(TcpListener listener) =>
        ((IPEndPoint)listener.LocalEndpoint).Port;
}
