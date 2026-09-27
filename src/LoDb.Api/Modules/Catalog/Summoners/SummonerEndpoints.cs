using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Images;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Catalog.Summoners;

/// <summary>The summoner spell list and the summoner spell page.</summary>
internal static class SummonerEndpoints
{
    private const string Segment = "summoner spell";

    public static void Map(RouteGroupBuilder catalog)
    {
        catalog.MapGet("/summoners", ListAsync)
            .WithName("listSummoners")
            .WithSummary("Summoner spell cards and their facet values; images may be pending.");
        catalog.MapGet("/summoners/{id}", GetAsync)
            .WithName("getSummoner")
            .WithSummary("A summoner spell's page, its image resolved.");
    }

    private static async Task<Results<CachedJson<SummonerList>, CatalogProblem>> ListAsync(
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
        var shown = page.Slice(SummonerList.EntriesOf(context.Catalog));
        var images = await context.ResolveAsync(
            shown.Select(EntityImages.Icon),
            ColdDemand.Queued,
            request.CancellationToken);
        return context.Answer(SummonerList.Of(context.Catalog, page, images), images);
    }

    private static async Task<Results<CachedJson<SummonerDetails>, CatalogProblem>> GetAsync(
        [AsParameters] DetailRequest request)
    {
        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        var id = CanonicalPath.IdOf(ResourceType.Summoners, request.Id);
        if (id is null || context.Catalog.Summoners.Find(id) is not { } spell)
        {
            return CatalogProblem.UnknownEntity(Segment, request.Id);
        }

        var images = await context.ResolveAsync(
            VersionImages.Of(spell),
            ColdDemand.Synchronous,
            request.CancellationToken);
        return context.Answer(SummonerDetails.Of(spell, context.Catalog, images), images);
    }
}
