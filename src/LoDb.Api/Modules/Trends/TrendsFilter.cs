using LoDb.Domain.Catalog.Modes;

namespace LoDb.Api.Modules.Trends;

/// <summary>
/// The filters the trends apply, as understood: an unknown mode or language is no filter,
/// as the legacy page ignored them.
/// </summary>
internal sealed record TrendsFilter
{
    public static TrendsFilter None { get; } = new();

    /// <summary>A champion id; the builds of any champion when null.</summary>
    public string? Champion { get; init; }

    public GameMode? Mode { get; init; }

    /// <summary>A Data Dragon language builds are written in.</summary>
    public string? Language { get; init; }
}
