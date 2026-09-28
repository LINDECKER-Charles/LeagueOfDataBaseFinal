namespace LoDb.Api.Modules.Billing.Http;

/// <summary>An opened Checkout session.</summary>
/// <param name="Url">Stripe's payment page, which the client navigates to.</param>
internal sealed record CheckoutCreated(Uri Url);
