using System.Collections.Frozen;
using LoDb.Api.Modules.Catalog.Http;
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
/// page tells what is wrong.
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
                + "state as a Server-Sent Event.");
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
        request.Context.Response.Headers[BufferingHeader] = NoBuffering;
        var scope = new WarmUpScope(request.Scope, resources);
        return TypedResults.ServerSentEvents(
            request.WarmUp.StreamAsync(scope, request.CancellationToken));
    }
}
