using LoDb.Api.Modules.Accounts.Protection;

namespace LoDb.Api.Modules.Builds.Http;

/// <summary>
/// A refused build call, written as a ProblemDetails whose <c>code</c> extension a client
/// switches on, with <c>errors</c> by field for the editor.
/// </summary>
/// <remarks>
/// A refused structure also lists, in <c>unavailableItems</c>, the names of the items its
/// mode excludes: the legacy message named them, and the editor still does.
/// </remarks>
internal sealed record BuildProblem : IResult
{
    private const string CodeExtension = "code";
    private const string UnavailableExtension = "unavailableItems";

    public required int Status { get; init; }

    /// <summary>Stable kebab-case code, the contract a client relies on.</summary>
    public required string Code { get; init; }

    public required string Title { get; init; }

    /// <summary>Codes of the invalid fields, by camelCase field name.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    /// <summary>Names of the items the build's mode excludes, in catalog order.</summary>
    public IReadOnlyList<string> UnavailableItems { get; init; } = [];

    public static BuildProblem Validation(
        IReadOnlyDictionary<string, string[]> errors,
        IReadOnlyList<string> unavailableItems) => new()
    {
        Status = StatusCodes.Status400BadRequest,
        Code = "validation-failed",
        Title = "Some fields are not valid.",
        Errors = errors,
        UnavailableItems = unavailableItems,
    };

    /// <summary>
    /// 404 for a build that is unknown, another account's, private where only public ones
    /// count, or named by a malformed token: the answer tells nothing about which.
    /// </summary>
    public static BuildProblem NotFound() => new()
    {
        Status = StatusCodes.Status404NotFound,
        Code = "build-not-found",
        Title = "No build answers to this link.",
    };

    /// <summary>
    /// 401 for a session whose account is gone or banned: it closes at its next revalidation,
    /// and meanwhile reads as signed out.
    /// </summary>
    public static BuildProblem SignedOut() => new()
    {
        Status = StatusCodes.Status401Unauthorized,
        Code = AccessDenials.AuthenticationRequired,
        Title = "Sign in first.",
    };

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var extensions = new Dictionary<string, object?> { [CodeExtension] = Code };
        if (Errors is null)
        {
            return TypedResults
                .Problem(statusCode: Status, title: Title, extensions: extensions)
                .ExecuteAsync(httpContext);
        }

        extensions[UnavailableExtension] = UnavailableItems;
        return TypedResults
            .ValidationProblem(Errors, title: Title, extensions: extensions)
            .ExecuteAsync(httpContext);
    }
}
