using LoDb.Api.Modules.Billing.Catalog;
using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Api.Modules.Billing.Fulfilment;
using LoDb.Infrastructure.Persistence.Billing.Payments;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Payments;

/// <summary>
/// Whether a paid session charged what the catalogue asks for what it sold, in euros: the
/// price of its pack or of its plan, an amount of the donation tiers.
/// </summary>
/// <remarks>
/// The site sets the amount of every session it opens: another amount or currency is no
/// session it opened as such, and nothing is granted for it.
/// </remarks>
internal static class CataloguePrices
{
    public static bool Match(SessionEvent paid)
    {
        ArgumentNullException.ThrowIfNull(paid);
        var session = paid.Session;
        if (session.AmountTotal is not { } amount
            || !string.Equals(
                session.Currency,
                CheckoutSessions.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return paid.Kind switch
        {
            StripeCheckoutKind.Pack => PackPrice(session) == amount,
            StripeCheckoutKind.Plan => PlanPrice(session) == amount,
            _ => DonationTiers.Allows(amount),
        };
    }

    // A pack is named by its requests, and each number of requests sells at one price.
    private static long? PackPrice(Session session)
    {
        var requests = SessionFields.Requests(session);
        return ApiPacks.All.FirstOrDefault(pack => pack.Requests == requests)?.PriceCents;
    }

    private static long? PlanPrice(Session session) =>
        ApiPlans.Find(SessionFields.Metadata(session, CheckoutMetadata.Plan))?.PriceCents;
}
