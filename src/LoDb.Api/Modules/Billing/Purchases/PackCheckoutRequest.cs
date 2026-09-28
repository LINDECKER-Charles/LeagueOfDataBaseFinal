namespace LoDb.Api.Modules.Billing.Purchases;

/// <summary>Body of <c>POST /api/billing/checkout/pack</c>.</summary>
internal sealed record PackCheckoutRequest
{
    /// <summary><c>small</c>, <c>medium</c> or <c>large</c>.</summary>
    public string? Pack { get; init; }

    /// <summary>Code of the locale of the portal, such as <c>fr</c>; English if unknown.</summary>
    public string? Locale { get; init; }
}
