using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Api.Modules.Billing.Fulfilment;
using LoDb.Infrastructure.Persistence.Billing.Payments;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Payments;

/// <summary>What a session sold, read from the <c>kind</c> of its metadata.</summary>
internal static class CheckoutKinds
{
    /// <summary>
    /// The kind of <paramref name="session"/>; null for one the site does not sell.
    /// </summary>
    /// <remarks>
    /// A session without any kind is a donation, as the legacy stack read it. Any other kind
    /// is left alone, where the legacy stack took it for a donation: it is no session the site
    /// opened.
    /// </remarks>
    public static StripeCheckoutKind? Of(Session session) =>
        SessionFields.Metadata(session, CheckoutMetadata.Kind) switch
        {
            CheckoutMetadata.PackKind => StripeCheckoutKind.Pack,
            CheckoutMetadata.PlanKind => StripeCheckoutKind.Plan,
            null or "" or CheckoutMetadata.DonationKind => StripeCheckoutKind.Donation,
            _ => null,
        };
}
