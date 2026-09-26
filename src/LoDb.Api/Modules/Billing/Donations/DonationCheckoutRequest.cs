namespace LoDb.Api.Modules.Billing.Donations;

/// <summary>Body of <c>POST /api/donations/checkout</c>.</summary>
internal sealed record DonationCheckoutRequest
{
    /// <summary>A tier or a free amount, in euro cents, from 100 to 50,000.</summary>
    public long? AmountCents { get; init; }

    /// <summary>
    /// Code of the locale of the page, such as <c>fr</c>, which Stripe's page names the
    /// donation in and the donor comes back to; English if unknown.
    /// </summary>
    public string? Locale { get; init; }
}
