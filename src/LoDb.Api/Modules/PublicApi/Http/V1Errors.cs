namespace LoDb.Api.Modules.PublicApi.Http;

/// <summary>
/// The codes of <c>/v1</c> and the refusals every route shares, word for word go-api's:
/// clients match on the code, some on the message.
/// </summary>
internal static class V1Errors
{
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string RateLimited = "rate_limited";
    public const string QuotaExceeded = "quota_exceeded";
    public const string InvalidRequest = "invalid_request";
    public const string Internal = "internal";

    public static V1Error MissingKey { get; } = new(
        StatusCodes.Status401Unauthorized,
        Unauthorized,
        "missing API key: use Authorization: Bearer <key> or X-Api-Key");

    public static V1Error MalformedKey { get; } = new(
        StatusCodes.Status401Unauthorized,
        Unauthorized,
        "malformed API key");

    public static V1Error UnknownKey { get; } = new(
        StatusCodes.Status401Unauthorized,
        Unauthorized,
        "unknown API key");

    public static V1Error RevokedKey { get; } = new(
        StatusCodes.Status403Forbidden,
        Forbidden,
        "API key is revoked or inactive");

    public static V1Error RateLimitExceeded { get; } = new(
        StatusCodes.Status429TooManyRequests,
        RateLimited,
        "rate limit exceeded, retry after X-RateLimit-Reset");

    public static V1Error QuotaExhausted { get; } = new(
        StatusCodes.Status429TooManyRequests,
        QuotaExceeded,
        "monthly quota exhausted and no credits left");

    /// <summary>The database or the storage failed: the request may succeed later.</summary>
    public static V1Error Unavailable { get; } = new(
        StatusCodes.Status503ServiceUnavailable,
        Internal,
        "service temporarily unavailable");

    /// <summary>Anything else: the cause is logged, never shown.</summary>
    public static V1Error InternalError { get; } = new(
        StatusCodes.Status500InternalServerError,
        Internal,
        "internal error");
}
