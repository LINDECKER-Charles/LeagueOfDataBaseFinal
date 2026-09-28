namespace LoDb.Domain.Editions;

/// <summary>
/// The entry of the other game that a reader may confuse an entry with: same name, twin id.
/// </summary>
public sealed record EditionTwin
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required Edition Edition { get; init; }
}
