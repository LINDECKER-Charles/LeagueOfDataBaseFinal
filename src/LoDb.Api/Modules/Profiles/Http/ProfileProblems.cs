using LoDb.Api.Modules.Accounts.Http;

namespace LoDb.Api.Modules.Profiles.Http;

/// <summary>
/// The refusals of the profile API, in the shape of the accounts API: a ProblemDetails whose
/// <c>code</c> a client switches on.
/// </summary>
internal static class ProfileProblems
{
    /// <summary>
    /// 404 for a profile that is unknown, private or banned alike: the answer tells nothing
    /// about which, nor whether the account exists.
    /// </summary>
    public static AccountProblem NotFound() => new()
    {
        Status = StatusCodes.Status404NotFound,
        Code = "profile-not-found",
        Title = "No public profile has this name.",
    };

    /// <summary>
    /// 409: replacing a password needs the current one, which this call never asks.
    /// </summary>
    public static AccountProblem PasswordExists() => new()
    {
        Status = StatusCodes.Status409Conflict,
        Code = "password-exists",
        Title = "The account already has a password.",
    };

    /// <summary>
    /// 401 for a session whose account is gone or banned: it closes at its next revalidation,
    /// and meanwhile reads as signed out, as <c>/api/account/me</c> does.
    /// </summary>
    public static AccountProblem SignedOut() => AccountProblem.AuthenticationRequired();
}
