using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Items.Details;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog;
using LoDb.Domain.Paths;
using LoDb.Ingestion.Catalog;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Catalog.Items;

/// <summary>The item list and the item page.</summary>
internal static class ItemEndpoints
{
    private const string Segment = "item";

    public static void Map(RouteGroupBuilder catalog)
    {
        catalog.MapGet("/items", ListAsync)
            .WithName("listItems")
            .WithSummary("Item cards and their facet values; images may be placeholders.");
        catalog.MapGet("/items/{id}", GetAsync)
            .WithName("getItem")
            .WithSummary("An item's page, its images resolved.");
    }

    private static async Task<Results<CachedJson<ItemList>, CatalogProblem>> ListAsync(
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
        var shown = page.Slice(ItemList.EntriesOf(context.Catalog));
        var images = await context.ResolveAsync(
            shown.Concat(ItemList.RelatedOf(shown, context.Catalog)).Select(EntityImages.Icon),
            ColdDemand.Queued,
            request.CancellationToken);
        return context.Answer(ItemList.Of(context.Catalog, page, images), images);
    }

    // Debris (placeholders, unnamed entries) has no page, though recipes show it (UP 10).
    private static async Task<Results<CachedJson<ItemDetails>, CatalogProblem>> GetAsync(
        [AsParameters] DetailRequest request)
    {
        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        var id = CanonicalPath.IdOf(ResourceType.Items, request.Id);
        if (id is null
            || context.Catalog.Items.Find(id) is not { } item
            || !context.Catalog.IsListed(item))
        {
            return CatalogProblem.UnknownEntity(Segment, request.Id);
        }

        var page = ItemPage.Of(context.Catalog, item);
        var images = await context.ResolveAsync(
            page.Images(),
            ColdDemand.Synchronous,
            request.CancellationToken);
        return context.Answer(ItemDetails.Of(page, images), images);
    }
}
