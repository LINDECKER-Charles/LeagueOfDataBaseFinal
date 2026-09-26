using System.Collections.Concurrent;
using LoDb.Api.Modules.Billing.Checkout;
using Stripe.Checkout;

namespace LoDb.Api.Tests.Billing.Support;

/// <summary>
/// Stripe's checkout as a double: it keeps the sessions it is asked to open and answers
/// with a payment page of its own, or fails as Stripe down would.
/// </summary>
public sealed class RecordingGateway : ICheckoutGateway
{
    public const string PageOrigin = "https://checkout.stripe.com";

    private readonly ConcurrentQueue<SessionCreateOptions> _sessions = new();

    public bool IsConfigured { get; set; } = true;

    /// <summary>Set, every call fails as when Stripe cannot be reached.</summary>
    public bool Failing { get; set; }

    /// <summary>The sessions asked for, in order.</summary>
    public IReadOnlyList<SessionCreateOptions> Sessions => [.. _sessions];

    public Task<Uri> CreateAsync(
        SessionCreateOptions options,
        CancellationToken cancellationToken)
    {
        if (Failing)
        {
            throw new CheckoutGatewayException("Stripe is down in this test.");
        }

        _sessions.Enqueue(options);
        return Task.FromResult(new Uri($"{PageOrigin}/c/pay/cs_test_{_sessions.Count}"));
    }
}
