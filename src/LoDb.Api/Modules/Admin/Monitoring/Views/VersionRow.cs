namespace LoDb.Api.Modules.Admin.Monitoring.Views;

/// <summary>A Data Dragon version and where its ingestion stands.</summary>
internal sealed record VersionRow
{
    public required string Version { get; init; }

    /// <summary>discovered, ingesting, ready or failed.</summary>
    public required string Status { get; init; }

    public required int Attempts { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
