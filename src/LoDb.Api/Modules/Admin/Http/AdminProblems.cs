using LoDb.Api.Modules.Accounts.Http;

namespace LoDb.Api.Modules.Admin.Http;

/// <summary>
/// The refusals of the admin API, as ProblemDetails whose <c>code</c> the admin front
/// switches on.
/// </summary>
internal static class AdminProblems
{
    public const string UserNotFound = "user-not-found";
    public const string BuildNotFound = "build-not-found";
    public const string ContactNotFound = "contact-not-found";
    public const string ApiClientNotFound = "api-client-not-found";

    /// <summary>409: an administrator may not ban or delete their own account.</summary>
    public const string SelfModeration = "self-moderation";

    /// <summary>409: the key is already revoked: nothing to revoke or credit.</summary>
    public const string ApiClientRevoked = "api-client-revoked";

    /// <summary>409: the account already has an authenticator.</summary>
    public const string MfaAlreadyEnrolled = "mfa-already-enrolled";

    public static AccountProblem NotFound(string code) => new()
    {
        Status = StatusCodes.Status404NotFound,
        Code = code,
        Title = "Nothing here bears this id.",
    };

    public static AccountProblem Conflict(string code) => new()
    {
        Status = StatusCodes.Status409Conflict,
        Code = code,
        Title = "The action does not apply to the current state.",
    };

    /// <summary>A validation problem naming one field.</summary>
    public static AccountProblem Invalid(string field, string code)
    {
        var errors = new FieldErrors();
        errors.Add(field, code);
        return errors.ToProblem();
    }
}
