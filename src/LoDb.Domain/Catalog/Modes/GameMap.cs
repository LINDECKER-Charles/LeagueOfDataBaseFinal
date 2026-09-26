namespace LoDb.Domain.Catalog.Modes;

/// <summary>
/// The Data Dragon maps that gate item availability (<c>"maps": {"11": true, …}</c>), valued
/// by their map id.
/// </summary>
/// <remarks>
/// Only the maps with a player-facing identity. Deliberately absent: 22 (TFT, never true on
/// any item) and 453, the Classic Rift, which is an edition rather than a map: every current
/// item carries that flag too (UP 6).
/// </remarks>
public enum GameMap
{
    SummonersRift = 11,
    HowlingAbyss = 12,
    NexusBlitz = 21,
    Arena = 30,
    Swarm = 33,
    Brawl = 35,
}
