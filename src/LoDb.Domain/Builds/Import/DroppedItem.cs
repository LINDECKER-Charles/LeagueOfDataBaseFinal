namespace LoDb.Domain.Builds.Import;

/// <summary>An item an import left out: gone from the target patch or off the mode's map.</summary>
public sealed record DroppedItem
{
    /// <summary>The index of its step in the source build, from 0.</summary>
    public required int Step { get; init; }

    public required string Id { get; init; }

    /// <summary>
    /// Its name on the target patch, classic items qualified by their id; the id itself when
    /// the target patch lacks it.
    /// </summary>
    public required string Name { get; init; }
}
