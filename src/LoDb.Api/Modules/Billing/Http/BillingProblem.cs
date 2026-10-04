using LoDb.Api.Modules.Accounts.Protection;

namespace LoDb.Api.Modules.Billing.Http;

/// <summary>
/// A refused payment call, written as a ProblemDetails whose <c>code</c> extension a client
/// switches on, with <c>errors</c> by field for a form.
/// </summary>
internal sealed record BillingProblem : IResult
{
    public const string Invalid = "invalid";

    private const string CodeExtension = "code";

    public required int Status { get; init; }

    /// <summary>Stable kebab-case code, the contract a client relies on.</summary>
    public required string Code { get; init; }

    public required string Title { get; init; }

    /// <summary>Codes of the invalid fields, by camelCase field name.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    /// <summary>400 for a field out of what is on sale, such as an amount or a pack.</summary>
    public static BillingProblem InvalidField(string field) => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Code = "validation-failed",
        Title = "Some fields are not valid.",
        Errors = new Dictionary<string, string[]> { [field] = [Invalid] },
    };

    /// <summary>
    /// 503 while no Stripe key, or no site origin to come back to, is set; the site's pages
    /// then show payments as unavailable.
    /// </summary>
    public static BillingProblem Unavailable() => new()
    {
        Status = StatusCodes.Status503ServiceUnavailable,
        Code = "payments-unavailable",
        Title = "Payments are not available.",
    };

    /// <summary>502 when Stripe refuses the session or cannot be reached.</summary>
    public static BillingProblem GatewayFailed() => new()
    {
        Status = StatusCodes.Status502BadGateway,
        Code = "gateway-failed",
        Title = "The payment service could not open the checkout.",
    };

    /// <summary>409 for a purchase without an active key for it to land on.</summary>
    public static BillingProblem ApiKeyRequired() => Conflict(
        "api-key-required",
        "Create an API key first.");

    /// <summary>409 for a second subscription: the first one has to end before.</summary>
    public static BillingProblem AlreadySubscribed() => Conflict(
        "already-subscribed",
        "The API key already has a subscription.");

    /// <summary>
    /// 401 for a session whose account is gone or banned: it closes at its next revalidation,
    /// and meanwhile reads as signed out.
    /// </summary>
    public static BillingProblem SignedOut() => new()
    {
        Status = StatusCodes.Status401Unauthorized,
        Code = AccessDenials.AuthenticationRequired,
        Title = "Sign in first.",
    };

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var extensions = new Dictionary<string, object?> { [CodeExtension] = Code };
        IResult problem = Errors is null
            ? TypedResults.Problem(statusCode: Status, title: Title, extensions: extensions)
            : TypedResults.ValidationProblem(Errors, title: Title, extensions: extensions);
        return problem.ExecuteAsync(httpContext);
    }

    private static BillingProblem Conflict(string code, string title) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Code = code,
        Title = title,
    };
}
