using System.Globalization;
using LoDb.Api.Modules.Accounts.Google;
using LoDb.Api.Modules.Accounts.Protection;

namespace LoDb.Api.Modules.Accounts.Http;

/// <summary>
/// A refused account call, written as a ProblemDetails whose <c>code</c> extension a client
/// switches on, with <c>errors</c> by field for a form, and <c>Retry-After</c> for a limit.
/// </summary>
internal sealed record AccountProblem : IResult
{
    private const string CodeExtension = "code";

    // A limit that just ran out still asks for one second: zero would invite a busy loop.
    private const double MinRetrySeconds = 1;

    public required int Status { get; init; }

    /// <summary>Stable kebab-case code, the contract a client relies on.</summary>
    public required string Code { get; init; }

    public required string Title { get; init; }

    public TimeSpan? RetryAfter { get; init; }

    /// <summary>Codes of the invalid fields, by camelCase field name.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static AccountProblem Validation(IReadOnlyDictionary<string, string[]> errors) => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Code = "validation-failed",
        Title = "Some fields are not valid.",
        Errors = errors,
    };

    public static AccountProblem InvalidCredentials() => Unauthorized(
        "invalid-credentials",
        "The identifier or the password is wrong.");

    public static AccountProblem TwoFactorRequired() => Unauthorized(
        "two-factor-required",
        "The account also needs a code of its authenticator app.");

    public static AccountProblem InvalidTwoFactorCode() => Unauthorized(
        "invalid-two-factor-code",
        "The authenticator or recovery code is wrong.");

    public static AccountProblem InvalidRefreshToken() => Unauthorized(
        "invalid-refresh-token",
        "The refresh token is expired, revoked or unreadable.");

    public static AccountProblem AuthenticationRequired() => Unauthorized(
        AccessDenials.AuthenticationRequired,
        "Sign in first.");

    public static AccountProblem AccountBanned() => Forbidden(
        "account-banned",
        "The account is banned.");

    public static AccountProblem Denied(string code) => Forbidden(
        code,
        "The request is not allowed.");

    public static AccountProblem AccountLocked(TimeSpan retryAfter) => TooManyRequests(
        "account-locked",
        "Too many failed sign-ins: the account is locked for a while.",
        retryAfter);

    public static AccountProblem ResendThrottled(TimeSpan retryAfter) => TooManyRequests(
        "resend-throttled",
        "Verification e-mails were sent too recently.",
        retryAfter);

    public static AccountProblem InvalidToken() => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Code = "invalid-token",
        Title = "The link is expired, already used or damaged.",
    };

    public static AccountProblem EmailAlreadyVerified() => Conflict(
        "email-already-verified",
        "The e-mail is already verified.");

    public static AccountProblem GoogleEmailUnverified() => Conflict(
        GoogleFailures.EmailUnverified,
        "Google does not vouch for this e-mail, which an account already uses.");

    public static AccountProblem GoogleFailed() => Conflict(
        GoogleFailures.Failed,
        "The Google account could not be tied to an account.");

    public static AccountProblem GoogleExchangeFailed() => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Code = "google-exchange-failed",
        Title = "Google refused the code or its verifier.",
    };

    public static AccountProblem GoogleUnavailable() => new()
    {
        Status = StatusCodes.Status503ServiceUnavailable,
        Code = GoogleFailures.Unavailable,
        Title = "Google sign-in is not available.",
    };

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (RetryAfter is { } delay)
        {
            var seconds = Math.Max(MinRetrySeconds, Math.Ceiling(delay.TotalSeconds));
            httpContext.Response.Headers.RetryAfter =
                seconds.ToString(CultureInfo.InvariantCulture);
        }

        var extensions = new Dictionary<string, object?> { [CodeExtension] = Code };
        IResult problem = Errors is null
            ? TypedResults.Problem(statusCode: Status, title: Title, extensions: extensions)
            : TypedResults.ValidationProblem(Errors, title: Title, extensions: extensions);
        return problem.ExecuteAsync(httpContext);
    }

    private static AccountProblem Unauthorized(string code, string title) => new()
    {
        Status = StatusCodes.Status401Unauthorized,
        Code = code,
        Title = title,
    };

    private static AccountProblem Forbidden(string code, string title) => new()
    {
        Status = StatusCodes.Status403Forbidden,
        Code = code,
        Title = title,
    };

    private static AccountProblem Conflict(string code, string title) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Code = code,
        Title = title,
    };

    private static AccountProblem TooManyRequests(string code, string title, TimeSpan retry) =>
        new()
        {
            Status = StatusCodes.Status429TooManyRequests,
            Code = code,
            Title = title,
            RetryAfter = retry,
        };
}
