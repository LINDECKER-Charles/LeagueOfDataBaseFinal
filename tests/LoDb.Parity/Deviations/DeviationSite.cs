namespace LoDb.Parity.Deviations;

/// <summary>Where a deviation is: a dataset entry, or a manifest key.</summary>
public sealed record DeviationSite
{
    public required string Version { get; init; }

    /// <summary>Null for a manifest, which is per version.</summary>
    public string? Language { get; init; }

    /// <summary><c>champions</c>…<c>summoners</c>, or <c>manifest/{type}</c>.</summary>
    public required string Resource { get; init; }

    /// <summary>The entry id or the manifest key; empty for the resource itself.</summary>
    public string Entry { get; init; } = "";
}
