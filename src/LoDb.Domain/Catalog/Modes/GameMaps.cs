namespace LoDb.Domain.Catalog.Modes;

/// <summary>
/// Reads the maps an item is available on from its <c>maps</c> flags.
/// </summary>
public static class GameMaps
{
    /// <summary>Every map, in declaration order.</summary>
    public static IReadOnlyList<GameMap> All { get; } = [.. Enum.GetValues<GameMap>()];

    /// <summary>Maps flagged true, in declaration order; none without flags.</summary>
    public static IReadOnlyList<GameMap> AvailableOn(IReadOnlyDictionary<int, bool>? maps) =>
        maps is null
            ? []
            : [.. All.Where(map => maps.TryGetValue((int)map, out var isAvailable) && isAvailable)];
}
