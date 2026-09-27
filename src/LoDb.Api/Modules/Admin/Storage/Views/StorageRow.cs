namespace LoDb.Api.Modules.Admin.Storage.Views;

/// <summary>A group of stored objects and its share of the bytes of its section.</summary>
internal sealed record StorageRow
{
    public required string Name { get; init; }

    public required long Objects { get; init; }

    public required long Bytes { get; init; }

    /// <summary>Percentage of the bytes of the section, from 0 to 100.</summary>
    public required double Pct { get; init; }
}
