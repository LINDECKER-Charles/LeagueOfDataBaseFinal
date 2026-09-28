namespace LoDb.Domain.Catalog.Modes;

/// <summary>
/// Game modes a build can target, each bound to the map that gates its items.
/// </summary>
/// <remarks>
/// A fixed subset of the persistent, named queues: Swarm and Brawl are event modes whose map
/// has no name in Data Dragon's own <c>map.json</c>, fine to filter a list on, not a stable
/// identity to build for. The codes (<see cref="GameModes.Code"/>) are persisted with the
/// builds and never change.
/// </remarks>
public enum GameMode
{
    SummonersRift,
    Aram,
    NexusBlitz,
    Arena,
}
