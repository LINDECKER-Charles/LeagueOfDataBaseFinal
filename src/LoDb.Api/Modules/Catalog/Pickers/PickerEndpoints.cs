using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog.Modes;
using LoDb.Ingestion.Catalog;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Catalog.Pickers;

/// <summary>
/// The projections of the build editor and the profile: short options, sorted by name, every
/// image resolved before the answer since a picker has no page to fill in later.
/// </summary>
internal static class PickerEndpoints
{
    public static void Map(RouteGroupBuilder pickers)
    {
        pickers.MapGet("/champions", ChampionsAsync)
            .WithName("pickChampions")
            .WithSummary("Every champion, by name.");
        pickers.MapGet("/items", ItemsAsync)
            .WithName("pickItems")
            .WithSummary("The items a build of the mode may carry, by name.");
        pickers.MapGet("/runes", RunesAsync)
            .WithName("pickRunes")
            .WithSummary("Every rune path, slot by slot.");
        pickers.MapGet("/summoners", SummonersAsync)
            .WithName("pickSummoners")
            .WithSummary("The summoner spells of Summoner's Rift, by name.");
        pickers.MapGet("/skins", SkinsAsync)
            .WithName("pickSkins")
            .WithSummary("A champion's skins, their art hotlinked; none for an unknown one.");
    }

    private static async Task<Results<CachedJson<ChampionPicker>, CatalogProblem>> ChampionsAsync(
        [AsParameters] PickerRequest request)
    {
        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        var images = await context.ResolveAsync(
            context.Catalog.Champions.Entries.Select(EntityImages.Portrait),
            ColdDemand.Synchronous,
            request.CancellationToken);
        return context.Answer(ChampionPicker.Of(context.Catalog, images), images);
    }

    // An unknown mode falls back to the default rather than failing, as the legacy picker
    // did: the answer names the mode it serves.
    private static async Task<Results<CachedJson<ItemPicker>, CatalogProblem>> ItemsAsync(
        [AsParameters] PickerRequest request,
        [FromQuery(Name = "mode")] string? mode)
    {
        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        var served = GameModes.Resolve(mode) ?? GameModes.Default;
        var images = await context.ResolveAsync(
            ItemPicker.Pickable(context.Catalog, served).Select(EntityImages.Icon),
            ColdDemand.Synchronous,
            request.CancellationToken);
        return context.Answer(ItemPicker.Of(context.Catalog, served, images), images);
    }

    private static async Task<Results<CachedJson<RunePicker>, CatalogProblem>> RunesAsync(
        [AsParameters] PickerRequest request)
    {
        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        var trees = context.Catalog.Runes.Entries;
        var images = await context.ResolveAsync(
            trees.Select(EntityImages.Icon).Concat(trees
                .SelectMany(static tree => tree.Slots)
                .SelectMany(static slot => slot.Runes)
                .Select(EntityImages.Icon)),
            ColdDemand.Synchronous,
            request.CancellationToken);
        return context.Answer(RunePicker.Of(context.Catalog, images), images);
    }

    private static async Task<Results<CachedJson<SummonerPicker>, CatalogProblem>> SummonersAsync(
        [AsParameters] PickerRequest request)
    {
        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        var images = await context.ResolveAsync(
            SummonerPicker.Pickable(context.Catalog).Select(EntityImages.Icon),
            ColdDemand.Synchronous,
            request.CancellationToken);
        return context.Answer(SummonerPicker.Of(context.Catalog, images), images);
    }

    // Skin art is hotlinked (UP 8): nothing to resolve.
    private static async Task<Results<CachedJson<SkinPicker>, CatalogProblem>> SkinsAsync(
        [AsParameters] PickerRequest request,
        [FromQuery(Name = "champion")] string? champion)
    {
        var read = await request.Gateway.ReadAsync(request.Scope, request.CancellationToken);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var context = read.Context;
        return context.Answer(SkinPicker.Of(context.Catalog, champion), ImageSet.Empty);
    }
}
