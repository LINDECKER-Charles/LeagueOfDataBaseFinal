using LoDb.Domain.Editions;

namespace LoDb.Api.Modules.Catalog.Shared;

/// <summary>
/// The same-named item or summoner spell of the other game (UP 6): "Faerie Charm" is both
/// 1004 and the Classic 771004.
/// </summary>
internal sealed record CounterpartLink
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required Edition Edition { get; init; }

    /// <summary>Its page; null when this catalog does not carry the twin.</summary>
    public string? CanonicalPath { get; init; }

    public static CounterpartLink Of(EditionTwin twin, string? canonicalPath)
    {
        ArgumentNullException.ThrowIfNull(twin);
        return new CounterpartLink
        {
            Id = twin.Id,
            Name = twin.Name,
            Edition = twin.Edition,
            CanonicalPath = canonicalPath,
        };
    }
}
