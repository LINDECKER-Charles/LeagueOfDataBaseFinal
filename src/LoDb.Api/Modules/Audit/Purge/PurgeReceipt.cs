namespace LoDb.Api.Modules.Audit.Purge;

/// <summary>What a purge deleted.</summary>
internal sealed record PurgeReceipt
{
    /// <summary>retention, before or all.</summary>
    public required string Scope { get; init; }

    /// <summary>First instant kept; null when everything went.</summary>
    public DateTimeOffset? Before { get; init; }

    /// <summary>Entries deleted; the entry recording the purge is not one of them.</summary>
    public required int Deleted { get; init; }
}
