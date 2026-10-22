using LoDb.Api.Modules.Catalog.Shared;
using LoDb.Domain.States;

namespace LoDb.Api.Modules.Catalog.States;

/// <summary>
/// A state of the entity in the block « Historique », read from the index alone: no
/// value, subjects without a name.
/// </summary>
internal sealed record StateHistoryEntry
{
    public required string OpenerVersion { get; init; }

    /// <summary>
    /// The last version of a closed state; null while open (« depuis le patch X »).
    /// </summary>
    public required string? LastVersion { get; init; }

    public required EntityChangeKind Kind { get; init; }

    public required IReadOnlyList<ChangedField> ChangedFields { get; init; }

    /// <summary>The state's canonical: its opener once closed, the short URL while open.</summary>
    public required CanonicalLink Link { get; init; }
}
