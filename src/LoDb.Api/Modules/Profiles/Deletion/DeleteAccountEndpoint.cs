using System.Security.Claims;
using LoDb.Api.Modules.Accounts.Authentication;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Modules.Accounts.Security.Admission;
using LoDb.Api.Modules.Accounts.Security.Limits;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Profiles.Deletion;

/// <summary>
/// <c>POST /api/profile/delete</c>: erases the signed-in account once confirmed, the right
/// to erasure the legal pages announce.
/// </summary>
/// <remarks>
/// <para>
/// The row goes, and the database removes what hangs on it: builds, votes and API keys go
/// with it, donations and messages lose their link. The session cookie closes; the tokens
/// of an app stop at their next refresh, which finds no account.
/// </para>
/// <para>
/// A wrong password counts toward the lockout, as at sign-in, and the confirmations of one
/// account are limited by <see cref="AccountRateLimits.PasswordConfirm"/>: by account, never
/// by address.
/// </para>
/// </remarks>
internal sealed class DeleteAccountEndpoint(
    ProfileOwners owners,
    UserManager<User> users,
    SignInManager<User> signIn,
    HashingAdmission admission,
    XsrfTokens xsrf,
    ProfileAudit audit,
    TimeProvider clock)
{
    public const string PasswordIncorrect = "password-incorrect";
    public const string ConfirmationIncorrect = "confirmation-incorrect";

    public static void Map(IEndpointRouteBuilder profile) =>
        profile.MapPost(
                "/delete",
                static (
                    [FromBody] DeleteAccountRequest request,
                    [FromServices] DeleteAccountEndpoint endpoint,
                    HttpContext context) => endpoint.DeleteAsync(request, context))
            .RequireRateLimiting(AccountRateLimits.PasswordConfirm)
            .AddEndpointFilter<HashingBusyFilter>()
            .WithName("deleteOwnAccount")
            .WithSummary("Erases the signed-in account once confirmed, and signs it out.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public async Task<Results<NoContent, AccountProblem>> DeleteAsync(
        DeleteAccountRequest request,
        HttpContext context)
    {
        if (await owners.FindAsync(context.User) is not { } owner)
        {
            return ProfileProblems.SignedOut();
        }

        if (await RefusalAsync(owner, request, context.RequestAborted) is { } refused)
        {
            return refused;
        }

        var deleted = await users.DeleteAsync(owner);
        if (!deleted.Succeeded)
        {
            throw new InvalidOperationException(
                $"Account {owner.Id} could not be erased: {deleted.Errors.First().Code}.");
        }

        // Recorded while the request still names the account, the actor of its own erasure.
        await audit.ErasedAsync(owner, context.RequestAborted);
        await SignOutAsync(context);
        return TypedResults.NoContent();
    }

    private async Task<AccountProblem?> RefusalAsync(
        User owner,
        DeleteAccountRequest request,
        CancellationToken cancellationToken)
    {
        switch (DeletionRules.ConfirmationOf(owner))
        {
            case DeletionConfirmation.Phrase:
                var locale = request.Locale ?? UiLocales.Fallback;
                return DeletionPhrases.Matches(request.Confirmation, locale)
                    ? null
                    : Refused(ProfileFields.Confirmation, ConfirmationIncorrect);
            case DeletionConfirmation.Password:
                return await PasswordRefusalAsync(owner, request.Password, cancellationToken);
            default:
                return null;
        }
    }

    // The step that hashes. A locked account is refused before any hash.
    private async Task<AccountProblem?> PasswordRefusalAsync(
        User owner,
        string? password,
        CancellationToken cancellationToken)
    {
        using var permit = await admission.EnterAsync(cancellationToken);
        var result = await signIn.CheckPasswordSignInAsync(
            owner,
            password ?? string.Empty,
            lockoutOnFailure: true);

        // Not allowed is a right password on a banned account: a ban never withholds the
        // erasure of the account's own data.
        if (result.Succeeded || result.IsNotAllowed)
        {
            return null;
        }

        return result.IsLockedOut
            ? AccountProblem.AccountLocked(LockoutTime.Left(owner, clock))
            : Refused(ProfileFields.Password, PasswordIncorrect);
    }

    // The apps forget their tokens themselves; a browser gets its cookie removed.
    private async Task SignOutAsync(HttpContext context)
    {
        if (AccountSchemes.IsBearer(context.Request))
        {
            return;
        }

        await signIn.SignOutAsync();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        xsrf.Issue(context);
    }

    private static AccountProblem Refused(string field, string code)
    {
        var errors = new FieldErrors();
        errors.Add(field, code);
        return errors.ToProblem();
    }
}
