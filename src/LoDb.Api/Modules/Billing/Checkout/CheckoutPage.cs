namespace LoDb.Api.Modules.Billing.Checkout;

/// <summary>What Stripe's payment page shows and where it sends the buyer back.</summary>
/// <param name="ProductName">The name of the only line, in the buyer's locale.</param>
/// <param name="SuccessUrl">Absolute URL once paid.</param>
/// <param name="CancelUrl">Absolute URL when the buyer turns back.</param>
internal sealed record CheckoutPage(string ProductName, string SuccessUrl, string CancelUrl);
