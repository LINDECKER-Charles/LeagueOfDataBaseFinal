using System.Text.Json.Nodes;

namespace LoDb.Parity.Projections;

/// <summary>The two projections of one (version, language): PHP and catalog exports.</summary>
public sealed record ProjectionPair
{
    public required string Version { get; init; }

    public required string Language { get; init; }

    public required JsonObject Legacy { get; init; }

    public required JsonObject Next { get; init; }
}
