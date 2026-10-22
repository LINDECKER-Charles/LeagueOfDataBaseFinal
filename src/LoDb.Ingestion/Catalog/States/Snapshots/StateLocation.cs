namespace LoDb.Ingestion.Catalog.States.Snapshots;

/// <summary>The state that decides a page of an entity, and how the index reached it.</summary>
public sealed record StateLocation
{
    public static StateLocation Outside { get; } = new() { Kind = StateLocationKind.Outside };

    public required StateLocationKind Kind { get; init; }

    /// <summary>
    /// The state; null when <see cref="Kind"/> is <see cref="StateLocationKind.Outside"/>, or
    /// <see cref="StateLocationKind.Covered"/> for an entity without a state at the version.
    /// Never null when <see cref="StateLocationKind.Lagging"/>.
    /// </summary>
    public IndexedState? State { get; init; }
}
