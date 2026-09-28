namespace LoDb.Api.Modules.Billing.Catalog;

/// <summary>
/// The amounts of a donation, the legacy <c>DonationTiers</c>: four tiers, or a free amount
/// from 1 to 500 euros.
/// </summary>
internal static class DonationTiers
{
    public const long MinCents = 100;
    public const long MaxCents = 50_000;

    /// <summary>The tiers the page offers, in cents, from the smallest.</summary>
    public static IReadOnlyList<long> Presets { get; } = [300, 500, 1_000, 2_500];

    public static bool Allows(long amountCents) => amountCents is >= MinCents and <= MaxCents;
}
