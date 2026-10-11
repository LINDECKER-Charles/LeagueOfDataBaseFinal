using System.Net;
using System.Text;
using LoDb.Api.Modules.Billing;
using LoDb.Api.Modules.Billing.Checkout;
using LoDb.Domain.Languages;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Tests.Billing;

/// <summary>
/// The Stripe gateway on the wire, Stripe's API answered by a handler of the test: the session
/// goes under the secret key with an idempotency key, and whatever goes wrong comes back as
/// a gateway failure.
/// </summary>
public sealed class StripeGatewayTests
{
    private const string SecretKey = "sk_test_placeholder_for_tests";
    private const string SessionsUrl = "https://api.stripe.com/v1/checkout/sessions";
    private const string PageUrl = "https://checkout.stripe.com/c/pay/cs_test_wire";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // How the session's fields are encoded is Stripe.net's; what they hold is
    // CheckoutSessionsTests'.
    [Fact]
    public async Task SessionIsPostedUnderTheSecretKeyOnce()
    {
        using var stripe = new StripeApi(HttpStatusCode.OK, Session(PageUrl));
        var gateway = Gateway(stripe);

        var url = await gateway.CreateAsync(Donation(), Cancellation);

        Assert.Equal(new Uri(PageUrl), url);
        var request = Assert.Single(stripe.Requests);
        Assert.Equal((HttpMethod.Post, SessionsUrl), (request.Method, request.Url));
        Assert.Equal("Bearer " + SecretKey, request.Authorization);
        Assert.False(string.IsNullOrEmpty(request.IdempotencyKey));
    }

    [Fact]
    public async Task RefusalOfStripeIsAGatewayFailure()
    {
        using var stripe = new StripeApi(
            HttpStatusCode.BadRequest,
            """{"error":{"type":"invalid_request_error","message":"Invalid amount."}}""");

        await Assert.ThrowsAsync<CheckoutGatewayException>(
            () => Gateway(stripe).CreateAsync(Donation(), Cancellation));
    }

    [Fact]
    public async Task SessionWithoutPageIsAGatewayFailure()
    {
        using var stripe = new StripeApi(HttpStatusCode.OK, Session(url: null));

        await Assert.ThrowsAsync<CheckoutGatewayException>(
            () => Gateway(stripe).CreateAsync(Donation(), Cancellation));
    }

    [Fact]
    public async Task UnreachableStripeIsAGatewayFailure()
    {
        using var stripe = new StripeApi(status: null, body: string.Empty);

        await Assert.ThrowsAsync<CheckoutGatewayException>(
            () => Gateway(stripe).CreateAsync(Donation(), Cancellation));
        Assert.Equal(3, stripe.Requests.Count);
    }

    [Fact]
    public void GatewayWithoutSecretKeyIsClosed()
    {
        using var stripe = new StripeApi(HttpStatusCode.OK, Session(PageUrl));

        Assert.False(Gateway(stripe, secretKey: " ").IsConfigured);
        Assert.True(Gateway(stripe).IsConfigured);
    }

    private static StripeCheckoutGateway Gateway(StripeApi stripe, string secretKey = SecretKey) =>
        new(Options.Create(new BillingOptions { StripeSecretKey = secretKey }), stripe);

    private static Stripe.Checkout.SessionCreateOptions Donation() =>
        CheckoutSessions.Donation(
            CheckoutPages.Donation("https://lodb.test", UiLocale.Fr),
            1_500,
            donorId: null);

    private static string Session(string? url) =>
        $$"""{"id":"cs_test_wire","object":"checkout.session","url":{{Json(url)}}}""";

    private static string Json(string? text) => text is null ? "null" : $"\"{text}\"";

    private sealed record SentRequest(
        HttpMethod Method,
        string Url,
        string? Authorization,
        string? IdempotencyKey);

    // Stripe's API: it answers every call the same, or fails as a network down when the
    // status is null, and keeps what it was sent.
    private sealed class StripeApi(HttpStatusCode? status, string body)
        : HttpMessageHandler, IHttpClientFactory
    {
        public List<SentRequest> Requests { get; } = [];

        public HttpClient CreateClient(string name)
        {
            Assert.Equal(StripeCheckoutGateway.HttpClientName, name);
            return new HttpClient(this, disposeHandler: false);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new SentRequest(
                request.Method,
                request.RequestUri!.GetLeftPart(UriPartial.Path),
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Idempotency-Key", out var keys)
                    ? keys.Single()
                    : null));
            if (status is not { } code)
            {
                throw new HttpRequestException("Stripe cannot be reached in this test.");
            }

            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
