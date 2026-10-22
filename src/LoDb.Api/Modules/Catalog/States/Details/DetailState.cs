using LoDb.Api.Modules.Catalog.Shared;

namespace LoDb.Api.Modules.Catalog.States.Details;

/// <summary>
/// What the index of states tells a detail about its entity: the page indexed for it, the
/// state that holds the page, the states before it, what followed it and what it changed.
/// </summary>
internal sealed record DetailState
{
    public required CanonicalLink Canonical { get; init; }

    public required EntityState? State { get; init; }

    public required IReadOnlyList<StateHistoryEntry> History { get; init; }

    public required StateNext? Next { get; init; }

    public required bool PresentInLatest { get; init; }

    public required EntityChanges? Changes { get; init; }
}
