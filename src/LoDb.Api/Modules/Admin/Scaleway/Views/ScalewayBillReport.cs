namespace LoDb.Api.Modules.Admin.Scaleway.Views;

/// <summary>
/// What Scaleway bills for the Object Storage of the backups and of the page store, read at
/// most once an hour, excluding VAT.
/// </summary>
/// <remarks>
/// Not configured: <c>configured</c> false, everything else empty. Unavailable: the last
/// reading failed and none succeeded before (<c>stale</c> true, no project). Stale: the last
/// reading failed, the previous good one is shown with its <c>fetchedAt</c>.
/// </remarks>
internal sealed record ScalewayBillReport
{
    public static ScalewayBillReport NotConfigured { get; } = new()
    {
        Configured = false,
        Stale = false,
    };

    /// <summary>Whether <c>LoDb:ScalewayBilling:SecretKey</c> is set.</summary>
    public required bool Configured { get; init; }

    /// <summary>Whether the last reading failed: what is shown, if anything, is older.</summary>
    public required bool Stale { get; init; }

    /// <summary>
    /// When the bill shown was read from Scaleway; null before any good reading. Scaleway
    /// tells no time of its own amounts: its <c>updated_at</c> is that of the query.
    /// </summary>
    public DateTimeOffset? FetchedAt { get; init; }

    /// <summary>ISO 4217 code of every amount; null before any good reading.</summary>
    public string? Currency { get; init; }

    public ScalewayProjectBill? Backups { get; init; }

    public ScalewayProjectBill? Pages { get; init; }
}
