using LoDb.Domain.Catalog;

namespace LoDb.Ingestion.Catalog.States.Storage;

/// <summary>A row of <c>catalog_entity_state</c>, as much of it as a walk resumes from.</summary>
internal sealed record StoredState
{
    public required ResourceType Resource { get; init; }

    public required string Key { get; init; }

    public required string Opener { get; init; }

    /// <summary>The last version of a closed state; null while open.</summary>
    public string? Last { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public required string Fingerprint { get; init; }
}
