using LoDb.Api.Tests.Billing.Support;

namespace LoDb.Api.Tests.Hosting.Support;

/// <summary>
/// The API of the request body limit tests on a real Kestrel, shared by them: Stripe is
/// configured so that the webhook reads its body. Each test sends one or two requests, below
/// any rate limit, and changes no state another one reads.
/// </summary>
public sealed class BodyLimitApi : IAsyncDisposable
{
    public KestrelApi Api { get; } = KestrelApi.Start(new Dictionary<string, string?>
    {
        ["LoDb:Billing:StripeSecretKey"] = BillingApp.SecretKey,
        ["LoDb:Billing:StripeWebhookSecret"] = BillingApp.WebhookSecret,
    });

    public ValueTask DisposeAsync() => Api.DisposeAsync();
}
