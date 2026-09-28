using System.Globalization;
using LoDb.Api.Modules.Billing.Catalog;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Checkout;

/// <summary>
/// The Checkout sessions the site opens, as the legacy <c>CheckoutSessionParams</c> and
/// <c>ApiCheckoutParams</c> build them: one line in euros, its price given inline, with no
/// product catalog on Stripe's side.
/// </summary>
internal static class CheckoutSessions
{
    /// <summary>Stripe replaces it with the id of the session in the success URL.</summary>
    public const string SessionIdSlot = "{CHECKOUT_SESSION_ID}";

    /// <summary>Everything is sold in euros; ISO 4217 code, as Stripe writes it.</summary>
    public const string Currency = "eur";

    private const string PaymentMode = "payment";
    private const string SubscriptionMode = "subscription";
    private const string DonateSubmit = "donate";

    /// <param name="page">The return URLs; the success one gets the session id appended.</param>
    /// <param name="amountCents">An amount <see cref="DonationTiers.Allows"/>.</param>
    /// <param name="donorId">The signed-in donor, whom the webhook marks as a supporter.</param>
    public static SessionCreateOptions Donation(CheckoutPage page, long amountCents, int? donorId)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new SessionCreateOptions
        {
            Mode = PaymentMode,
            SubmitType = DonateSubmit,
            LineItems = [Line(page.ProductName, amountCents)],
            SuccessUrl = page.SuccessUrl + "?session_id=" + SessionIdSlot,
            CancelUrl = page.CancelUrl,
            ClientReferenceId = donorId is { } id ? Text(id) : null,
            Metadata = new Dictionary<string, string>
            {
                [CheckoutMetadata.Source] = CheckoutMetadata.DonationSource,
                [CheckoutMetadata.Kind] = CheckoutMetadata.DonationKind,
            },
        };
    }

    public static SessionCreateOptions Pack(CheckoutPage page, ApiPackTerms pack, int buyerId)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(pack);
        var options = Purchase(page, Line(page.ProductName, pack.PriceCents), buyerId);
        options.Mode = PaymentMode;
        options.Metadata[CheckoutMetadata.Kind] = CheckoutMetadata.PackKind;
        options.Metadata[CheckoutMetadata.Requests] =
            pack.Requests.ToString(CultureInfo.InvariantCulture);
        return options;
    }

    public static SessionCreateOptions Plan(CheckoutPage page, ApiPlanTerms plan, int buyerId)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(plan);
        var line = Line(page.ProductName, plan.PriceCents);
        line.PriceData.Recurring = new SessionLineItemPriceDataRecurringOptions
        {
            Interval = plan.Interval,
        };
        var options = Purchase(page, line, buyerId);
        options.Mode = SubscriptionMode;
        options.Metadata[CheckoutMetadata.Kind] = CheckoutMetadata.PlanKind;
        options.Metadata[CheckoutMetadata.Plan] = plan.Code;
        return options;
    }

    // A purchase of the API names its buyer twice, as the legacy did: the webhook reads the
    // metadata first.
    private static SessionCreateOptions Purchase(
        CheckoutPage page,
        SessionLineItemOptions line,
        int buyerId) => new()
    {
        LineItems = [line],
        SuccessUrl = page.SuccessUrl,
        CancelUrl = page.CancelUrl,
        ClientReferenceId = Text(buyerId),
        Metadata = new Dictionary<string, string> { [CheckoutMetadata.UserId] = Text(buyerId) },
    };

    private static SessionLineItemOptions Line(string productName, long amountCents) => new()
    {
        Quantity = 1,
        PriceData = new SessionLineItemPriceDataOptions
        {
            Currency = Currency,
            UnitAmount = amountCents,
            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = productName },
        },
    };

    private static string Text(int id) => id.ToString(CultureInfo.InvariantCulture);
}
