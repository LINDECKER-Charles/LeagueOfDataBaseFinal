using LoDb.Api.Modules.Catalog.Champions.Details;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.ClientPolicy.Versions;
using LoDb.Domain.Catalog.Champions;
using LoDb.Ingestion.Catalog;
using LoDb.Ingestion.Images;
using LoDb.Ingestion.Images.Art;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

namespace LoDb.Api.Modules.Catalog.Champions;

/// <summary>The champion list, the champion page and its skins.</summary>
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
        catalog.MapGet("/champions/{id}/skins", SkinsAsync)
            .WithName("getChampionSkins")
            .WithSummary("A champion's skins and their chromas, every art hotlinked.");
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
        var shown = page.Slice(ChampionList.EntriesOf(context.Catalog));
        var portraits = await context.ResolveAsync(
            shown.Select(EntityImages.Portrait),
            ColdDemand.Queued,
            request.CancellationToken);
        var images = await context.WithStoredArtAsync(
            portraits,
            shown.SelectMany(ArtVariants.LoadingOf),
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
        if (Find(context, request.Id) is not { } champion)
        {
            return CatalogProblem.UnknownEntity(Segment, request.Id);
        }

        var canonical = await request.LocateCanonicalAsync(
            context.Catalog.Version,
            context.Catalog.PathOf(champion));
        if (!canonical.IsLocated)
        {
            return canonical.Problem;
        }

        var images = await ImagesOfAsync(context, champion, request.CancellationToken);
        var page = ChampionDetails.Of(champion, context.Catalog, images);
        var details = page with { Canonical = canonical.Link ?? page.Canonical };
        // An app gets more than the web: no cache may hand one the other's page.
        request.Context.Response.Headers.Append(HeaderNames.Vary, ClientHeader.Name);
        return context.Answer(
            IsApp(request.Context) ? details.WithSkins(champion, context.Catalog) : details,
            images);
    }

    // The icons of the version, then the art variants the store already holds.
    private static async Task<ImageSet> ImagesOfAsync(
        CatalogContext context,
        ChampionDetail champion,
        CancellationToken cancellationToken)
    {
        var icons = await context.ResolveAsync(
            VersionImages.Of(champion),
            ColdDemand.Synchronous,
            cancellationToken);
        return await context.WithStoredArtAsync(
            icons,
            ArtVariants.Of(champion),
            cancellationToken);
    }

    // The art is hotlinked (UP 8): nothing to resolve.
    private static async Task<Results<CachedJson<ChampionSkins>, CatalogProblem>> SkinsAsync(
        [AsParameters] DetailRequest request)
    {
        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        return Find(context, request.Id) is { } champion
            ? context.Answer(ChampionSkins.Of(champion, context.Catalog), ImageSet.Empty)
            : CatalogProblem.UnknownEntity(Segment, request.Id);
    }

    // An installed app names itself on every call (ADR 0008); the web and the renderer never do.
    private static bool IsApp(HttpContext context) =>
        ClientHeader.Read(context.Request) is not null;

    // The id, or the champion's id or name typed loosely ("wukong"): the page's canonical path
    // then differs from the segment, and the renderer moves it there with a 301.
    private static ChampionDetail? Find(CatalogContext context, string segment) =>
        context.Catalog.FindChampion(segment);
}
