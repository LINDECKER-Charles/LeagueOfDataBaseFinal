namespace LoDb.Api.Modules.Trends;

/// <summary>
/// A page of the trends, with the filters it applied and the facets to change them.
/// </summary>
internal sealed record TrendsPage
{
    public required IReadOnlyList<TrendRow> Rows { get; init; }

    /// <summary>How many builds match the filters, on every page.</summary>
    public required int Total { get; init; }

    public required int Page { get; init; }

    /// <summary>At least 1, even without any build.</summary>
    public required int Pages { get; init; }

    public required int PerPage { get; init; }

    public required TrendsFilter Filters { get; init; }

    /// <summary>The champions of the public builds, by name.</summary>
    public required IReadOnlyList<ChampionOption> ChampionOptions { get; init; }

    /// <summary>The languages of the public builds, by code.</summary>
    public required IReadOnlyList<string> LanguageOptions { get; init; }
}
