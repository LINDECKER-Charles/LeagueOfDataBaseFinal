namespace LoDb.Infrastructure.Analytics.Retention;

/// <summary>What a retention run changed.</summary>
public sealed record RetentionSummary
{
    /// <summary>Partitions created ahead of their day.</summary>
    public required int PartitionsCreated { get; init; }

    /// <summary>Partitions dropped with their views, past the retention.</summary>
    public required int PartitionsDropped { get; init; }

    /// <summary>Views whose address and user agent were erased.</summary>
    public required int ClientDataErased { get; init; }
}
