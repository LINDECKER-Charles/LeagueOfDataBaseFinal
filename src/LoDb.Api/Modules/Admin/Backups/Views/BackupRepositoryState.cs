namespace LoDb.Api.Modules.Admin.Backups.Views;

/// <summary>
/// The restic repository as its last retention measured it, and what storing it is estimated
/// to cost each month at the configured price.
/// </summary>
/// <remarks>
/// An estimate below the bill: the bucket also keeps, for up to a month, the versions that
/// the retention deleted and the object lock still holds.
/// </remarks>
internal sealed record BackupRepositoryState
{
    /// <summary>Bytes stored, compressed and deduplicated, every snapshot included.</summary>
    public required long Bytes { get; init; }

    public required DateTimeOffset MeasuredAt { get; init; }

    /// <summary>Price of one GB (10^9 bytes) stored for a month.</summary>
    public required decimal PricePerGbMonth { get; init; }

    /// <summary>ISO 4217 code of the prices.</summary>
    public required string Currency { get; init; }

    /// <summary>The bytes in GB times the price, to four decimals.</summary>
    public required decimal MonthlyCost { get; init; }
}
