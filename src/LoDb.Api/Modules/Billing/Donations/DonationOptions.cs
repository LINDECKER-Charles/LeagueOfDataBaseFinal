namespace LoDb.Api.Modules.Billing.Donations;

/// <summary>What the donation page offers.</summary>
internal sealed record DonationOptions
{
    /// <summary>False while payments are closed: the page then says donations are coming.</summary>
    public required bool Available { get; init; }

    /// <summary>The tiers, in cents, from the smallest.</summary>
    public required IReadOnlyList<long> Presets { get; init; }

    /// <summary>The smallest free amount, in cents.</summary>
    public required long MinCents { get; init; }

    /// <summary>The largest free amount, in cents.</summary>
    public required long MaxCents { get; init; }

    /// <summary>ISO 4217 code, lowercase as Stripe writes it: <c>eur</c>.</summary>
    public required string Currency { get; init; }
}
