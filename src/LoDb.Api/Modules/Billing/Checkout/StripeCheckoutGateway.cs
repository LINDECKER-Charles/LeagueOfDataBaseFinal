using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace LoDb.Api.Modules.Billing.Checkout;

/// <summary>
/// The gateway on Stripe's API, through Stripe.net over a named <see cref="HttpClient"/>: the
/// tests put a fake handler behind it, so no call leaves the machine.
/// </summary>
internal sealed class StripeCheckoutGateway(
    IOptions<BillingOptions> settings,
    IHttpClientFactory httpClients) : ICheckoutGateway
{
    /// <summary>Name of the <see cref="HttpClient"/> the calls to Stripe go through.</summary>
    public const string HttpClientName = "stripe";

    // Stripe.net sends an idempotency key with each call, so a retried creation opens one
    // session only.
    private const int MaxNetworkRetries = 2;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.Value.StripeSecretKey);

    public async Task<Uri> CreateAsync(
        SessionCreateOptions options,
        CancellationToken cancellationToken)
    {
        var service = new SessionService(Client());
        try
        {
            var session = await service.CreateAsync(options, null, cancellationToken);
            return Uri.TryCreate(session.Url, UriKind.Absolute, out var url)
                ? url
                : throw new CheckoutGatewayException("Stripe opened a session without a page.");
        }
        catch (Exception exception) when (IsGatewayFailure(exception, cancellationToken))
        {
            throw new CheckoutGatewayException("Stripe could not open the session.", exception);
        }
    }

    private StripeClient Client()
    {
        var secret = settings.Value.StripeSecretKey;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("No Stripe secret key is set.");
        }

        var http = new SystemNetHttpClient(
            httpClients.CreateClient(HttpClientName),
            MaxNetworkRetries,
            appInfo: null,
            enableTelemetry: false);
        return new StripeClient(secret, httpClient: http);
    }

    // A timeout surfaces as a cancellation the caller did not ask for.
    private static bool IsGatewayFailure(Exception exception, CancellationToken cancellation) =>
        exception is StripeException or HttpRequestException
        || (exception is TaskCanceledException && !cancellation.IsCancellationRequested);
}
