using System.Collections.Frozen;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Analytics.Capture;

namespace LoDb.Api.Modules.Analytics.Pages;

/// <summary>
/// The counted pages under the names the legacy stack logged them with
/// (<c>RequestEventFactory::ROUTES</c>), so that its aggregates and the new ones add up.
/// </summary>
internal static class TrackedRoutes
{
    public const string HomeKind = "home";
    public const string ListKind = "list";
    public const string DetailKind = "detail";

    private const string HomeRoute = "app_home";
    private const string HomeType = "home";

    private static readonly FrozenDictionary<ResourceType, Names> ByResource =
        new Dictionary<ResourceType, Names>
        {
            [ResourceType.Champions] = new("app_champions", "app_champion", "champion"),
            [ResourceType.Items] = new("app_items", "app_item", "item"),
            [ResourceType.Runes] = new("app_runes", "app_rune", "runesReforged"),
            [ResourceType.Summoners] = new("app_summoners", "app_summoner", "summoner"),
        }.ToFrozenDictionary();

    /// <summary>The page of <paramref name="address"/>, as the front answers it.</summary>
    /// <param name="address">The address requested.</param>
    /// <param name="lang">The Data Dragon language the page shows.</param>
    /// <param name="answer">What the front answers: status, entity, version.</param>
    public static TrackedPage Page(PageAddress address, string lang, PageAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(answer);
        var (route, type, kind) = address.Resource is not { } resource
            ? (HomeRoute, HomeType, HomeKind)
            : NamesOf(resource, address.Entry is not null);
        return new TrackedPage
        {
            Route = route,
            Path = address.Path,
            Type = type,
            Kind = kind,
            Entity = answer.Entity,
            Status = answer.Status,
            Version = answer.Version,
            Lang = lang,
            Locale = UiLocales.Code(address.Locale),
        };
    }

    private static (string Route, string Type, string Kind) NamesOf(
        ResourceType resource,
        bool detail)
    {
        var names = ByResource[resource];
        return detail
            ? (names.DetailRoute, names.Type, DetailKind)
            : (names.ListRoute, names.Type, ListKind);
    }

    private sealed record Names(string ListRoute, string DetailRoute, string Type);
}
