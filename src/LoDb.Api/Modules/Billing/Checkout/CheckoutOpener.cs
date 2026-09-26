using LoDb.Api.Modules.Accounts.Links;
using LoDb.Api.Modules.Billing.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Checkout;

/// <summary>
/// Opens the sessions of the checkout endpoints: payments are open once a Stripe key and the
/// site's origin, which the buyer comes back to, are both set.
/// </summary>
/// <remarks>
/// The origin is the configured one, never the request's <c>Host</c>, as for the links of the
/// e-mails.
/// </remarks>
internal sealed partial class CheckoutOpener(
    ICheckoutGateway gateway,
    LinkOrigin origin,
    ILogger<CheckoutOpener> logger)
{
    /// <summary>Whether a checkout can be opened now.</summary>
    public bool IsOpen => SiteOrigin() is not null;

    /// <summary>The origin to come back to; null while payments are closed.</summary>
    public string? SiteOrigin() => gateway.IsConfigured ? origin.Resolve() : null;

    /// <param name="options">The session, built by <see cref="CheckoutSessions"/>.</param>
    /// <param name="kind">Its <c>kind</c>, for the logs, which name no buyer.</param>
    /// <param name="cancellationToken">Aborts the call to Stripe.</param>
    public async Task<Results<Ok<CheckoutCreated>, BillingProblem>> OpenAsync(
        SessionCreateOptions options,
        string kind,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = await gateway.CreateAsync(options, cancellationToken);
            LogOpened(logger, kind);
            return TypedResults.Ok(new CheckoutCreated(url));
        }
        catch (CheckoutGatewayException exception)
        {
            LogFailed(logger, kind, exception);
            return BillingProblem.GatewayFailed();
        }
    }

    [LoggerMessage(
        EventName = "billing.checkout.opened",
        Level = LogLevel.Information,
        Message = "Checkout session of kind {Kind} opened.")]
    private static partial void LogOpened(ILogger logger, string kind);

    [LoggerMessage(
        EventName = "billing.checkout.failed",
        Level = LogLevel.Warning,
        Message = "Stripe could not open a checkout session of kind {Kind}.")]
    private static partial void LogFailed(ILogger logger, string kind, Exception exception);
}
