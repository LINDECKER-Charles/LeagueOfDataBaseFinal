using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Registration;
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
    AccountAudit audit)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/reset-password",
                static (
                    [FromBody] ResetPasswordRequest request,
                    [FromServices] ResetPasswordEndpoint endpoint,
                    CancellationToken aborted) => endpoint.ResetAsync(request, aborted))
            .WithName("resetPassword")
            .WithSummary("Sets a new password with the parameters of the link sent by e-mail.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

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

        var user = link.Account;
        var reset = await users.ResetPasswordAsync(user, link.Token, request.Password!);
        if (!reset.Succeeded)
        {
            return AccountProblem.InvalidToken();
        }

        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, lockoutEnd: null);
        await audit.RecordAsync(AuditAction.UserPasswordReset, user, cancellationToken);
        return TypedResults.NoContent();
    }

    private static AccountProblem? WeakPassword(string? password)
    {
        var errors = new FieldErrors();
        RegistrationRules.CheckPassword(errors, password);
        return errors.IsEmpty ? null : errors.ToProblem();
    }
}
