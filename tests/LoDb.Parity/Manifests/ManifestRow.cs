namespace LoDb.Parity.Manifests;

/// <summary>A <c>ddragon_asset</c> row, as the collection copies it.</summary>
public sealed record ManifestRow
{
    public required string Version { get; init; }

    public required string Type { get; init; }

    public required string Key { get; init; }

    /// <summary><c>present</c> or <c>absent</c>.</summary>
    public required string Status { get; init; }

    public string? Sha256 { get; init; }

    public string? Extension { get; init; }
}
