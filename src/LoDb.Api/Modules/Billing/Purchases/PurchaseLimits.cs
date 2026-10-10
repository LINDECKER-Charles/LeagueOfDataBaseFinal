using LoDb.Api.Modules.Accounts.Security.Limits;

namespace LoDb.Api.Modules.Billing.Purchases;

/// <summary>
/// The Checkout sessions an account may open for credit packs and plans: each one costs up
/// to three calls to Stripe, whose rate limit the merchant account shares with every buyer.
/// </summary>
internal static class PurchaseLimits
{
    private const int CheckoutsPerHour = 10;

    /// <summary>Pack and plan checkouts together, 10 an hour per account by default.</summary>
    public static AccountActionLimit Checkout { get; } =
        new(Policy: "purchase-checkout", Setting: "CheckoutsPerHour", CheckoutsPerHour);
}
