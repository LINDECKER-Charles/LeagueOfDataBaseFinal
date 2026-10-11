using LoDb.Infrastructure.Analytics.Capture;

namespace LoDb.Infrastructure.Tests.Audience.Support;

/// <summary>
/// The catalog's reading of a few addresses, as the API's resolver answers them: Ahri's detail
/// in two locales, a sword's detail, a list, a detail the catalog lacks; anything else, a
/// pinned version among them, is no counted page.
/// </summary>
internal sealed class CatalogPages : ITrackedPageResolver
{
    public const string AhriFr = "/fr/champions/103-ahri";
    public const string AhriEn = "/en/champions/103-ahri";
    public const string Sword = "/fr/items/1036-long-sword";
    public const string Champions = "/fr/champions";
    public const string Missing = "/fr/items/9999-nothing";
    public const string Pinned = "/fr/16.1.1/items/1036-long-sword";

    private const short Ok = 200;
    private const short NotFound = 404;
    private const short Unavailable = 503;

    private static readonly Dictionary<string, TrackedPage> Pages = new(StringComparer.Ordinal)
    {
        [AhriFr] = Detail(AhriFr, "champion", "Ahri", Ok),
        [AhriEn] = Detail(AhriEn, "champion", "Ahri", Ok),
        [Sword] = Detail(Sword, "item", "1036", Ok),
        [Missing] = Detail(Missing, "item", "9999", NotFound),
        [Champions] = Detail(Champions, "champion", null, Ok) with { Kind = "list" },
    };

    /// <summary>
    /// Data Dragon out of reach, as the API's resolver then answers: every known page a 503.
    /// </summary>
    public bool IsCatalogUnavailable { get; set; }

    public Task<TrackedPage?> ResolveAsync(string target, CancellationToken cancellationToken) =>
        Task.FromResult(Pages.GetValueOrDefault(target) is { } page && IsCatalogUnavailable
            ? page with { Status = Unavailable }
            : Pages.GetValueOrDefault(target));

    private static TrackedPage Detail(string path, string type, string? entity, short status) =>
        new()
        {
            Route = "app_" + type,
            Path = path,
            Type = type,
            Kind = "detail",
            Entity = entity,
            Status = status,
            Locale = path.Split('/')[1],
        };
}
