using LoDb.Parity.Deviations;

namespace LoDb.Parity.Manifests;

/// <summary>
/// The two manifests of one (version, type): <c>manifest/{version}/{type}.json</c> of the
/// legacy storage, and the <c>ddragon_asset</c> rows of the new stack.
/// </summary>
public sealed record ManifestPair
{
    public required string Version { get; init; }

    /// <summary>The legacy manifest name, also the <c>type</c> column of the new rows.</summary>
    public required string Type { get; init; }

    /// <summary>Key to blob path (<c>cdn/blobs/{sha}.{ext}</c>), or null for absent.</summary>
    public required IReadOnlyDictionary<string, string?> Legacy { get; init; }

    public required IReadOnlyDictionary<string, ManifestRow> Next { get; init; }

    /// <summary>The <see cref="DeviationTags"/> of each key, from the projections.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Tags { get; init; } =
        new Dictionary<string, IReadOnlySet<string>>();
}
