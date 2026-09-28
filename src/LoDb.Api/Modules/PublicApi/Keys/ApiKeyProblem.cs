using LoDb.Api.Modules.Accounts.Protection;

namespace LoDb.Api.Modules.PublicApi.Keys;

/// <summary>
/// A refused call of the API key portal, written as a ProblemDetails whose <c>code</c>
/// extension a client switches on.
/// </summary>
internal sealed record ApiKeyProblem : IResult
{
    private const string CodeExtension = "code";

    public required int Status { get; init; }

    /// <summary>Stable kebab-case code, the contract a client relies on.</summary>
    public required string Code { get; init; }

    public required string Title { get; init; }

    /// <summary>409 for a second key: the account's active one has to go first.</summary>
    public static ApiKeyProblem KeyExists() => new()
    {
        Status = StatusCodes.Status409Conflict,
        Code = "api-key-exists",
        Title = "The account already has an active API key.",
    };

    /// <summary>404 for a regeneration or a revocation without an active key.</summary>
    public static ApiKeyProblem NoKey() => new()
    {
        Status = StatusCodes.Status404NotFound,
        Code = "api-key-missing",
        Title = "The account has no active API key.",
    };

    /// <summary>
    /// 401 for a session whose account is gone or banned: it closes at its next revalidation,
    /// and meanwhile reads as signed out.
    /// </summary>
    public static ApiKeyProblem SignedOut() => new()
    {
        Status = StatusCodes.Status401Unauthorized,
        Code = AccessDenials.AuthenticationRequired,
        Title = "Sign in first.",
    };

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return TypedResults.Problem(
                statusCode: Status,
                title: Title,
                extensions: new Dictionary<string, object?> { [CodeExtension] = Code })
            .ExecuteAsync(httpContext);
    }
}
