namespace LoDb.Api.Modules.Billing;

/// <summary>Settings of the payments (<c>LoDb:Billing</c>).</summary>
/// <remarks>
/// Both secrets come from the environment, never from a committed file. Unset or blank, the
/// matching side is off: checkouts answer 503, and so does the webhook, so that Stripe keeps
/// its events until it is set.
/// </remarks>
internal sealed class BillingOptions
{
    public const string SectionName = "LoDb:Billing";

    /// <summary>
    /// Stripe's secret API key (<c>sk_…</c>), the legacy <c>STRIPE_SECRET_KEY</c>.
    /// </summary>
    public string? StripeSecretKey { get; set; }

    /// <summary>
    /// Secret the webhook signatures are checked with (<c>whsec_…</c>), the legacy
    /// <c>STRIPE_WEBHOOK_SECRET</c>.
    /// </summary>
    public string? StripeWebhookSecret { get; set; }
}
