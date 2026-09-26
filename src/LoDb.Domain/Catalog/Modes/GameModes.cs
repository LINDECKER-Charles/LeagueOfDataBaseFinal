using System.Collections.Frozen;

namespace LoDb.Domain.Catalog.Modes;

/// <summary>
/// Persisted codes of the build modes and the map each one plays on.
/// </summary>
/// <remarks>
/// The codes come from an explicit table, not a naming policy: "sr" is no casing of
/// "SummonersRift", and they are frozen by the builds already stored.
/// </remarks>
public static class GameModes
{
    /// <summary>Mode of a build that names none (legacy builds, forms without script).</summary>
    public const GameMode Default = GameMode.SummonersRift;

    private static readonly FrozenDictionary<GameMode, (string Code, GameMap Map)> Table =
        new Dictionary<GameMode, (string, GameMap)>
        {
            [GameMode.SummonersRift] = ("sr", GameMap.SummonersRift),
            [GameMode.Aram] = ("aram", GameMap.HowlingAbyss),
            [GameMode.NexusBlitz] = ("nexus_blitz", GameMap.NexusBlitz),
            [GameMode.Arena] = ("arena", GameMap.Arena),
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, GameMode> ByCode = Table.ToFrozenDictionary(
        entry => entry.Value.Code,
        entry => entry.Key,
        StringComparer.Ordinal);

    /// <summary>Every mode, in declaration order.</summary>
    public static IReadOnlyList<GameMode> All { get; } = [.. Enum.GetValues<GameMode>()];

    /// <summary>Persisted code of the mode ("sr", "nexus_blitz").</summary>
    public static string Code(GameMode mode) => Table[mode].Code;

    /// <summary>Reads a code exactly as <see cref="Code"/> writes it.</summary>
    public static bool TryParse(string? code, out GameMode mode) =>
        ByCode.TryGetValue(code ?? string.Empty, out mode);

    /// <summary>
    /// Lenient reading of a requested mode: absent or blank means <see cref="Default"/>, an
    /// unknown value is <see langword="null"/> so the caller can reject it.
    /// </summary>
    public static GameMode? Resolve(string? requested)
    {
        var code = requested?.Trim() ?? string.Empty;
        if (code.Length == 0)
        {
            return Default;
        }

        return TryParse(code, out var mode) ? mode : null;
    }

    /// <summary>Map whose <c>maps</c> flag decides item availability in the mode.</summary>
    public static GameMap MapOf(GameMode mode) => Table[mode].Map;
}
