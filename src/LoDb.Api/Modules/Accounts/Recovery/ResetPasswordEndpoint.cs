using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Registration;
using LoDb.Api.Modules.Accounts.Security.Admission;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Accounts.Recovery;

/// <summary>
/// <c>POST /api/account/reset-password</c>: sets a new password with the parameters of the
/// link, and lifts a lockout.
/// </summary>
/// <remarks>
/// The new password changes the security stamp: the link cannot serve twice, the refresh
/// tokens of the account stop working, and its open sessions close at their next check.
/// </remarks>
internal sealed class ResetPasswordEndpoint(
    UserManager<User> users,
    ResetLinks links,
    HashingAdmission admission,
    AccountAudit audit)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/reset-password",
                static (
                    [FromBody] ResetPasswordRequest request,
                    [FromServices] ResetPasswordEndpoint endpoint,
                    CancellationToken aborted) => endpoint.ResetAsync(request, aborted))
            .AddEndpointFilter<HashingBusyFilter>()
            .WithName("resetPassword")
            .WithSummary("Sets a new password with the parameters of the link sent by e-mail.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public async Task<Results<NoContent, AccountProblem>> ResetAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (await links.ReadAsync(request.UserId, request.Token) is not { } link)
        {
            return AccountProblem.InvalidToken();
        }

        if (WeakPassword(request.Password) is { } weak)
        {
            return weak;
        }

        if (!await ResetPasswordAsync(link, request.Password!, cancellationToken))
        {
            return AccountProblem.InvalidToken();
        }

        var user = link.Account;

        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, lockoutEnd: null);
        await audit.RecordAsync(AuditAction.UserPasswordReset, user, cancellationToken);
        return TypedResults.NoContent();
    }

    // The step that hashes the new password.
    private async Task<bool> ResetPasswordAsync(
        ResetLink link,
        string password,
        CancellationToken cancellationToken)
    {
        using var permit = await admission.EnterAsync(cancellationToken);
        return (await users.ResetPasswordAsync(link.Account, link.Token, password)).Succeeded;
    }

    private static AccountProblem? WeakPassword(string? password)
    {
        var errors = new FieldErrors();
        RegistrationRules.CheckPassword(errors, password);
        return errors.IsEmpty ? null : errors.ToProblem();
    }
}
