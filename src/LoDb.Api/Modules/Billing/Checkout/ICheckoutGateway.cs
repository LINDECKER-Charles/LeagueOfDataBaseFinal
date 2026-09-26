using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Checkout;

/// <summary>Opens Checkout sessions on Stripe; replaced by a double in the tests.</summary>
internal interface ICheckoutGateway
{
    /// <summary>False while no secret key is set: payments are then unavailable.</summary>
    bool IsConfigured { get; }

    /// <summary>Opens the session and returns the URL of its payment page.</summary>
    /// <exception cref="CheckoutGatewayException">
    /// Stripe refused it or could not be reached.
    /// </exception>
    Task<Uri> CreateAsync(SessionCreateOptions options, CancellationToken cancellationToken);
}
