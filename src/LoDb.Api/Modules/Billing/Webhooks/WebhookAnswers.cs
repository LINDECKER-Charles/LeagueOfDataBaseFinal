using Microsoft.AspNetCore.Http.HttpResults;

namespace LoDb.Api.Modules.Billing.Webhooks;

/// <summary>What the webhook answers Stripe, which only reads the status.</summary>
internal static class WebhookAnswers
{
    private const string CodeExtension = "code";

    public static Ok<WebhookReceipt> Received() =>
        TypedResults.Ok(new WebhookReceipt(Received: true));

    /// <summary>503: Stripe keeps the event and delivers it again once a secret is set.</summary>
    public static ProblemHttpResult Unconfigured() => Problem(
        StatusCodes.Status503ServiceUnavailable,
        "webhook-unconfigured",
        "The webhook is not configured.");

    /// <summary>400: a payload Stripe did not sign, or signed too long ago.</summary>
    public static ProblemHttpResult Rejected() => Problem(
        StatusCodes.Status400BadRequest,
        "invalid-signature",
        "The payload or its signature is not valid.");

    /// <summary>500: nothing was kept, and Stripe delivers the event again.</summary>
    public static ProblemHttpResult Failed() => Problem(
        StatusCodes.Status500InternalServerError,
        "handler-failed",
        "The event could not be applied.");

    private static ProblemHttpResult Problem(int status, string code, string title) =>
        TypedResults.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?> { [CodeExtension] = code });
}
