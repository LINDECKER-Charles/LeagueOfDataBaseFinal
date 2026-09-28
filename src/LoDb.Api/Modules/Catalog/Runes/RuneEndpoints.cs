using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Images;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Catalog.Runes;

/// <summary>The rune list and the rune path page.</summary>
internal static class RuneEndpoints
{
    private const string Segment = "rune path";

    public static void Map(RouteGroupBuilder catalog)
    {
        catalog.MapGet("/runes", ListAsync)
            .WithName("listRunes")
            .WithSummary("Rune cards, paginated over the runes, and every path.");
        catalog.MapGet("/runes/{id}", GetAsync)
            .WithName("getRuneTree")
            .WithSummary("A rune path's page, its images resolved.");
    }

    private static async Task<Results<CachedJson<RuneList>, CatalogProblem>> ListAsync(
        [AsParameters] ListRequest request)
    {
        if (PageRequest.From(request.Page, request.Size) is not { } page)
        {
            return CatalogProblem.InvalidPage();
        }

        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        var catalog = context.Catalog;
        var shown = page.Slice(RuneList.Located(catalog));
        var images = await context.ResolveAsync(
            RuneList.TreesOf(catalog).Select(EntityImages.Icon)
                .Concat(shown.Select(location => EntityImages.Icon(location.Rune))),
            ColdDemand.Queued,
            request.CancellationToken);
        return context.Answer(RuneList.Of(catalog, page, images), images);
    }

    private static async Task<Results<CachedJson<RuneTreeDetails>, CatalogProblem>> GetAsync(
        [AsParameters] DetailRequest request)
    {
        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        var id = CanonicalPath.IdOf(ResourceType.Runes, request.Id);
        if (id is null || context.Catalog.Runes.Find(id) is not { } tree)
        {
            return CatalogProblem.UnknownEntity(Segment, request.Id);
        }

        var images = await context.ResolveAsync(
            VersionImages.Of(tree),
            ColdDemand.Synchronous,
            request.CancellationToken);
        return context.Answer(RuneTreeDetails.Of(tree, context.Catalog, images), images);
    }
}
