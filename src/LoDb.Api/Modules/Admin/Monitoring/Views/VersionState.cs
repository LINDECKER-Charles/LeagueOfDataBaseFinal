namespace LoDb.Api.Modules.Admin.Monitoring.Views;

/// <summary>The Data Dragon versions known to the database.</summary>
internal sealed record VersionState
{
    /// <summary>The version served as the latest; null before the first promotion.</summary>
    public string? Current { get; init; }

    public DateTimeOffset? PromotedAt { get; init; }

    public required int Discovered { get; init; }

    public required int Ingesting { get; init; }

    public required int Ready { get; init; }

    public required int Failed { get; init; }

    /// <summary>The versions changed last, the latest change first.</summary>
    public required IReadOnlyList<VersionRow> Recent { get; init; }
}
