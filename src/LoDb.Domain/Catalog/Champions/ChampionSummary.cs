namespace LoDb.Domain.Catalog.Champions;

/// <summary>
/// A champion as the list dataset (<c>champion.json</c>) describes it.
/// </summary>
/// <remarks>
/// <see cref="Id"/> is the stable identifier of URLs and art ("MonkeyKing"),
/// <see cref="Key"/> the numeric one the game uses ("62"). Old versions lack
/// <see cref="Partype"/> and ship other stat keys (UP 3): absence stays a value.
/// </remarks>
public sealed record ChampionSummary
{
    public required string Id { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>Title as Data Dragon writes it: never lowercased, whatever the language.</summary>
    public required string Title { get; init; }

    public required string Blurb { get; init; }

    /// <summary>Square portrait file name ("Ahri.png").</summary>
    public required string Image { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>Resource bar name in the dataset language, when the version ships one.</summary>
    public string? Partype { get; init; }

    public ChampionRatings? Info { get; init; }

    /// <summary>Base stats under their Data Dragon keys ("attackrange", "hp"…).</summary>
    public required IReadOnlyDictionary<string, double> Stats { get; init; }
}
