namespace LoDb.Infrastructure.Analytics.Capture;

/// <summary>
/// A page whose views are counted, described as the legacy stack described its routes: the
/// columns of <c>analytics_event</c> that follow from the requested address alone.
/// </summary>
public sealed record TrackedPage
{
    /// <summary>Name of the legacy route, such as <c>app_champion</c>.</summary>
    public required string Route { get; init; }

    /// <summary>Path of the page, without its query.</summary>
    public required string Path { get; init; }

    /// <summary>
    /// <c>home</c>, <c>champion</c>, <c>item</c>, <c>runesReforged</c> or <c>summoner</c>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary><c>home</c>, <c>list</c> or <c>detail</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Legacy key of the entity of a detail page (<c>Ahri</c>, <c>1004</c>,
    /// <c>Domination</c>, <c>SummonerFlash</c>): reports count <c>{type}:{key}</c>.
    /// </summary>
    public string? Entity { get; init; }

    /// <summary>HTTP status the page answers with.</summary>
    public required short Status { get; init; }

    /// <summary>Data Dragon version shown, when the address pins one.</summary>
    public string? Version { get; init; }

    /// <summary>Data Dragon language shown, such as <c>fr_FR</c>.</summary>
    public string? Lang { get; init; }

    /// <summary>Interface locale, such as <c>fr</c>.</summary>
    public required string Locale { get; init; }
}
