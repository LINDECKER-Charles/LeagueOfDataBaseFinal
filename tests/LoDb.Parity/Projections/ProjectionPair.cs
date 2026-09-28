using System.Text.Json.Nodes;

namespace LoDb.Parity.Projections;

/// <summary>The two projections of one (version, language): PHP and catalog exports.</summary>
public sealed record ProjectionPair
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    public required JsonObject Legacy { get; init; }

    public required JsonObject Next { get; init; }

    /// <summary>
    /// The items whose en_US name declares a placeholder, read from the new en_US projection
    /// of the version: a translated name no longer says so (ar_AE, zh_CN).
    /// </summary>
    public IReadOnlySet<string> Placeholders { get; init; } = new HashSet<string>();
}
