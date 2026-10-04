using System.Text;
using LoDb.Api.Modules.Billing.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Billing.Webhooks;

/// <summary>
/// <c>POST /webhooks/stripe</c>: receives Stripe's events, signed with the webhook secret.
/// </summary>
/// <remarks>
/// Stripe only reads the status: 503 while no secret is set and 500 when a handler fails, so
/// that it delivers again later; 400 for a payload it did not sign, which it drops; 200 for
/// anything recorded, even a type the site ignores or a redelivery. Outside of <c>/api</c>, the
/// forgery guard lets it through, and it needs no account.
/// </remarks>
internal sealed partial class StripeWebhookEndpoint(
    IOptions<BillingOptions> options,
    WebhookSignature signature,
    WebhookProcessor processor,
    ILogger<StripeWebhookEndpoint> logger)
{
    private const string SignatureHeader = "Stripe-Signature";

    // Stripe's events weigh a few kilobytes: a larger body is no event of theirs.
    private const long MaxPayloadBytes = 1024 * 1024;

    public static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                BillingRoutes.StripeWebhook,
                static ([FromServices] StripeWebhookEndpoint endpoint, HttpContext context) =>
                    endpoint.ReceiveAsync(context))
            .WithMetadata(new RequestSizeLimitAttribute(MaxPayloadBytes))
            .ExcludeFromDescription();

    public async Task<Results<Ok<WebhookReceipt>, ProblemHttpResult>> ReceiveAsync(
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var secret = options.Value.StripeWebhookSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            LogUnconfigured(logger);
            return WebhookAnswers.Unconfigured();
        }

        var payload = await ReadAsync(context.Request);
        var header = context.Request.Headers[SignatureHeader].ToString();
        if (signature.Verify(payload, header, secret) is not { } stripeEvent)
        {
            LogRejected(logger);
            return WebhookAnswers.Rejected();
        }

        var outcome = await processor.ProcessAsync(stripeEvent, context.RequestAborted);
        if (outcome is WebhookOutcome.Failed)
        {
            return WebhookAnswers.Failed();
        }

        return WebhookAnswers.Received();
    }

    // The signature covers the bytes as sent: the body is read whole, as UTF-8.
    private static async Task<string> ReadAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync(request.HttpContext.RequestAborted);
    }

    [LoggerMessage(
        EventName = "billing.webhook.unconfigured",
        Level = LogLevel.Critical,
        Message = "A Stripe event came in while no webhook secret is set: answered 503.")]
    private static partial void LogUnconfigured(ILogger logger);

    [LoggerMessage(
        EventName = "billing.webhook.rejected",
        Level = LogLevel.Warning,
        Message = "A payload posted as a Stripe event has no valid signature: answered 400.")]
    private static partial void LogRejected(ILogger logger);
}
