namespace LoDb.Api.Modules.Analytics.Pages;

/// <summary>
/// What the front answers a counted page with: its status and, on a detail, the legacy key
/// of the entity it names.
/// </summary>
/// <param name="Status">HTTP status of the page.</param>
/// <param name="Entity">Legacy key of a detail's entity; null on a home page or a list.</param>
internal sealed record PageAnswer(short Status, string? Entity)
{
    /// <summary>The version a home page pins by its query; the other pages never do.</summary>
    public string? Version { get; init; }

    /// <summary>The page renders.</summary>
    public static PageAnswer Found(string? entity) =>
        new((short)StatusCodes.Status200OK, entity);

    /// <summary>The detail names no entity of the latest version.</summary>
    public static PageAnswer Missing(string entity) =>
        new((short)StatusCodes.Status404NotFound, entity);

    /// <summary>No catalog to show: no version ingested yet, or Data Dragon down.</summary>
    public static PageAnswer Unavailable(string? entity) =>
        new((short)StatusCodes.Status503ServiceUnavailable, entity);
}
