using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Domain.Catalog.Summoners;
using LoDb.Domain.Derived.Ranges;
using LoDb.Ingestion.Catalog.Snapshots;

namespace LoDb.Api.Modules.Catalog.Summoners;

/// <summary>
/// <c>GET /api/catalog/{version}/{lang}/summoners/{id}</c>: the card, then the cost, range
/// and charges, the image resolved.
/// </summary>
internal sealed record SummonerDetails
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    public string? ContentLanguage { get; init; }

    /// <summary>The page the routing redirects a stale or slugless URL to.</summary>
    public required string CanonicalPath { get; init; }

    /// <summary>The facts of the list's card, the edition and its twin among them.</summary>
    public required SummonerCard Profile { get; init; }

    public required IReadOnlyList<double> Cost { get; init; }

    /// <summary>What the cost is paid in, when Data Dragon says.</summary>
    public string? CostType { get; init; }

    /// <summary>Range per rank; see <see cref="GlobalRange"/>.</summary>
    public required IReadOnlyList<double> Range { get; init; }

    /// <summary>Whether the range is Data Dragon's "whole map" sentinel.</summary>
    public required bool GlobalRange { get; init; }

    public int? Charges { get; init; }

    public static SummonerDetails Of(
        SummonerSpell spell,
        CatalogSnapshot catalog,
        ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(spell);
        ArgumentNullException.ThrowIfNull(catalog);
        var profile = SummonerCard.Of(spell, catalog, images);
        return new SummonerDetails
        {
            Version = catalog.Version.Value,
            Language = catalog.Language.Code,
            ContentLanguage = catalog.Summoners.ContentLanguage?.Code,
            CanonicalPath = profile.CanonicalPath,
            Profile = profile,
            Cost = spell.Cost,
            CostType = spell.CostType,
            Range = spell.Range,
            GlobalRange = spell.Range.Any(RangeSentinels.IsGlobal),
            Charges = spell.Charges,
        };
    }
}
