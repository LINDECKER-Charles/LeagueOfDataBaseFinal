using LoDb.Api.Hosting;

namespace LoDb.Api.Modules.Billing.Http;

/// <summary>Paths and OpenAPI tags of the payments.</summary>
internal static class BillingRoutes
{
    /// <summary>The donation page's tiers and whether donations are open.</summary>
    public const string DonationOptions = ApiPaths.App + "/donations/options";

    /// <summary>Opens the Checkout session of a donation.</summary>
    public const string DonationCheckout = ApiPaths.App + "/donations/checkout";

    /// <summary>The credit packs and plans of the public API on sale.</summary>
    public const string Offers = ApiPaths.App + "/billing/offers";

    /// <summary>Opens the Checkout session of a credit pack.</summary>
    public const string PackCheckout = ApiPaths.App + "/billing/checkout/pack";

    /// <summary>Opens the Checkout session of a subscription.</summary>
    public const string PlanCheckout = ApiPaths.App + "/billing/checkout/plan";

    /// <summary>
    /// Where Stripe posts its events, outside of <c>/api</c> as with the legacy stack: nginx
    /// routes <c>/webhooks/</c> to the API, and the app's OpenAPI document leaves it out.
    /// </summary>
    public const string StripeWebhook = "/webhooks/stripe";

    /// <summary>The generated client gets one service per tag.</summary>
    public const string DonationsTag = "Donations";

    public const string BillingTag = "Billing";
}
