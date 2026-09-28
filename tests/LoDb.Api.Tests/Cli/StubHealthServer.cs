using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LoDb.Api.Tests.Cli;

/// <summary>
/// A real server on a free loopback port whose <c>/healthz</c> answers a chosen status, for
/// the Docker probe to reach over the network as it does in the container.
/// </summary>
internal sealed class StubHealthServer : IAsyncDisposable
{
    private const string AnyFreeLoopbackPort = "http://127.0.0.1:0";

    private readonly WebApplication _app;

    private StubHealthServer(WebApplication app) => _app = app;

    /// <summary>The port the server listens on.</summary>
    public int Port => new Uri(_app.Urls.Single()).Port;

    public static async Task<StubHealthServer> StartAsync(
        int status,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(AnyFreeLoopbackPort);
        var app = builder.Build();
        app.MapGet("/healthz", () => Results.StatusCode(status));
        await app.StartAsync(cancellationToken);
        return new StubHealthServer(app);
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
