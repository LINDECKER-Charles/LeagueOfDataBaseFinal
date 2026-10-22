using LoDb.Domain.Versions;

namespace LoDb.Ingestion.Catalog.States.Snapshots;

/// <summary>A generation of the index as a snapshot is built from it.</summary>
public sealed record StateIndexGeneration
{
    public required long Id { get; init; }

    /// <summary>Its fingerprint scheme (<c>FingerprintScheme</c>).</summary>
    public required int Scheme { get; init; }

    /// <summary>When it was started: the date of a rebuilt history.</summary>
    public required DateTimeOffset BuiltAt { get; init; }

    /// <summary>The covered versions, oldest first: a run of consecutive listed versions.</summary>
    public required IReadOnlyList<PatchVersion> Versions { get; init; }
}
