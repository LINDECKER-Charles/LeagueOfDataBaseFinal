using LoDb.Api.Modules.Catalog.Champions.Details;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Images;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Catalog.Champions;

/// <summary>The champion list and the champion page.</summary>
internal static class ChampionEndpoints
{
    private const string Segment = "champion";

    public static void Map(RouteGroupBuilder catalog)
    {
        catalog.MapGet("/champions", ListAsync)
            .WithName("listChampions")
            .WithSummary("Champion cards and their facet values; images may be placeholders.");
        catalog.MapGet("/champions/{id}", GetAsync)
            .WithName("getChampion")
            .WithSummary("A champion's page, its images resolved.");
    }

    private static async Task<Results<CachedJson<ChampionList>, CatalogProblem>> ListAsync(
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
        var shown = page.Slice(context.Catalog.Champions.Entries);
        var images = await context.ResolveAsync(
            shown.Select(EntityImages.Portrait),
            ColdDemand.Queued,
            request.CancellationToken);
        return context.Answer(ChampionList.Of(context.Catalog, page, images), images);
    }

    private static async Task<Results<CachedJson<ChampionDetails>, CatalogProblem>> GetAsync(
        [AsParameters] DetailRequest request)
    {
        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        var id = CanonicalPath.IdOf(ResourceType.Champions, request.Id);
        if (id is null || context.Catalog.Champions.Find(id) is not { } champion)
        {
            return CatalogProblem.UnknownEntity(Segment, request.Id);
        }

        var images = await context.ResolveAsync(
            VersionImages.Of(champion),
            ColdDemand.Synchronous,
            request.CancellationToken);
        return context.Answer(ChampionDetails.Of(champion, context.Catalog, images), images);
    }
}
