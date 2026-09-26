namespace LoDb.Api.Modules.Admin.Donations;

/// <summary>The counters of the donations, over every gift.</summary>
internal sealed record DonationKpis
{
    public required long TotalCents { get; init; }

    public required int Count { get; init; }

    /// <summary>Distinct accounts that gave while signed in.</summary>
    public required int IdentifiedDonors { get; init; }

    /// <summary>Gifts without an account.</summary>
    public required int Anonymous { get; init; }

    /// <summary>Accounts marked supporter.</summary>
    public required int Supporters { get; init; }
}
