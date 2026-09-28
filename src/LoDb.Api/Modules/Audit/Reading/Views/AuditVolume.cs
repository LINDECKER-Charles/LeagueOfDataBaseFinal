namespace LoDb.Api.Modules.Audit.Reading.Views;

/// <summary>What the journal holds, and what the retention keeps of it.</summary>
internal sealed record AuditVolume
{
    public required long Entries { get; init; }

    /// <summary>Time of the oldest entry; null when the journal is empty.</summary>
    public DateTimeOffset? Oldest { get; init; }

    /// <summary>Time of the newest entry; null when the journal is empty.</summary>
    public DateTimeOffset? Newest { get; init; }

    /// <summary>Size of the table with its indexes, in bytes.</summary>
    public required long TotalBytes { get; init; }

    /// <summary>How long the daily retention keeps an entry: 6 months.</summary>
    public required int RetentionMonths { get; init; }

    /// <summary>The first instant the retention keeps now.</summary>
    public required DateTimeOffset RetentionCutoff { get; init; }
}
