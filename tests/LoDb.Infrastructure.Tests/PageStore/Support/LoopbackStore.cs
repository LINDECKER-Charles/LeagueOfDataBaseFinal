using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LoDb.Infrastructure.Tests.PageStore.Support;

/// <summary>
/// A real server on a free loopback port that stands for the store, so the <c>page-store</c>
/// client is exercised with the wire it is registered with: each request is kept with its
/// headers, then answered by <c>answer</c>.
/// </summary>
internal sealed class LoopbackStore : IAsyncDisposable
{
    private const string AnyFreeLoopbackPort = "http://127.0.0.1:0";

    private readonly WebApplication _app;
    private readonly ConcurrentQueue<IReadOnlyDictionary<string, string>> _requests;

    private LoopbackStore(
        WebApplication app,
        ConcurrentQueue<IReadOnlyDictionary<string, string>> requests)
    {
        _app = app;
        _requests = requests;
    }

    /// <summary>The base URL the store listens on.</summary>
    public string Endpoint => _app.Urls.Single();

    /// <summary>The headers of every request received so far, by lower-case name.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Requests => [.. _requests];

    public static async Task<LoopbackStore> StartAsync(
        Action<HttpContext> answer,
        CancellationToken cancellationToken)
    {
        var requests = new ConcurrentQueue<IReadOnlyDictionary<string, string>>();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(AnyFreeLoopbackPort);
        var app = builder.Build();
        app.Run(context =>
        {
            requests.Enqueue(context.Request.Headers.ToDictionary(
                static header => header.Key.ToLowerInvariant(),
                static header => header.Value.ToString(),
                StringComparer.Ordinal));
            answer(context);
            return Task.CompletedTask;
        });
        await app.StartAsync(cancellationToken);
        return new LoopbackStore(app, requests);
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
