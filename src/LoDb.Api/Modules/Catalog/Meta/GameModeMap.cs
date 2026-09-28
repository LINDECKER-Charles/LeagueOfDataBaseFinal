using LoDb.Domain.Catalog.Modes;

namespace LoDb.Api.Modules.Catalog.Meta;

/// <summary>A game mode and the Data Dragon map id that gates its items.</summary>
internal sealed record GameModeMap
{
    public required GameMode Mode { get; init; }

    public required int Map { get; init; }
}
