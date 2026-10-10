using System.Collections.Frozen;
using LoDb.Api.Hosting;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.WarmUp.Limits;
using LoDb.Api.Modules.Catalog.WarmUp.Progress;
using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Catalog.WarmUp;

/// <summary>
/// The loader's stream: Server-Sent Events whose every frame is the whole state of the
/// warm-up, until a done or failed frame ends it.
/// </summary>
/// <remarks>
/// A version or language the gateway refuses ends the stream with a failed frame carrying
/// the problem code a catalog call would answer: the client visits the page anyway, and the
/// page tells what is wrong. So does a stream that finds no place
/// (<see cref="WarmUpStreams"/>), with <c>catalog-pending</c>. A client starting too many
/// gets a 429 (<see cref="ColdWorkRateLimit"/>), which the loader treats the same way.
/// </remarks>
internal static class WarmUpEndpoint
{
    // nginx buffers a proxied answer unless the upstream opts out: a buffered frame is a
    // frozen progress bar.
    private const string BufferingHeader = "X-Accel-Buffering";
    private const string NoBuffering = "no";

    private static readonly FrozenDictionary<string, ResourceType> BySegment =
        Enum.GetValues<ResourceType>()
            .ToFrozenDictionary(CanonicalPath.SegmentOf, StringComparer.Ordinal);

    public static void Map(RouteGroupBuilder catalog)
    {
        catalog.MapGet("/warm-up", Stream)
            .WithName("warmUpCatalog")
            .WithSummary(
                "Ingests the datasets, then the images the named lists show, streaming each "
                + "state as a Server-Sent Event.")
            .RequireRateLimiting(ColdWorkRateLimit.Policy);
    }

    private static Results<ServerSentEventsResult<WarmUpProgress>, CatalogProblem> Stream(
        [AsParameters] WarmUpRequest request)
    {
        var named = request.Resources ?? [];
        if (named.FirstOrDefault(static value => !BySegment.ContainsKey(value)) is { } unknown)
        {
            return CatalogProblem.InvalidResource(unknown);
        }

        var resources = named.Select(static value => BySegment[value]).Distinct().ToList();
        var response = request.Context.Response;
        response.Headers[BufferingHeader] = NoBuffering;
        var client = RateLimitingPolicies.ClientKey(request.Context.Connection.RemoteIpAddress);
        if (request.Streams.TryOpen(client) is not { } place)
        {
            return TypedResults.ServerSentEvents(NoPlaceAsync(resources));
        }

        // Released with the request, however the stream ends.
        response.RegisterForDispose(place);
        var scope = new WarmUpScope(request.Scope, resources);
        return TypedResults.ServerSentEvents(
            request.WarmUp.StreamAsync(scope, request.CancellationToken));
    }

    private static async IAsyncEnumerable<WarmUpProgress> NoPlaceAsync(
        IReadOnlyList<ResourceType> resources)
    {
        await Task.CompletedTask;
        yield return WarmUpProgress.Failed(resources, CatalogProblem.Pending().Code);
    }
}
