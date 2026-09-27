namespace LoDb.Api.Modules.Admin.Donations;

/// <summary>
/// A page of the donations, newest first, with their counters and the daily totals of the
/// last thirty days.
/// </summary>
internal sealed record AdminDonationPage
{
    public required DonationKpis Kpis { get; init; }

    /// <summary>One entry per day, oldest first, days without gifts at zero.</summary>
    public required IReadOnlyList<DailyAmount> Daily { get; init; }

    public required IReadOnlyList<AdminDonationRow> Items { get; init; }

    public required int Total { get; init; }

    /// <summary>The page, from 1.</summary>
    public required int Page { get; init; }

    public required int Pages { get; init; }
}
