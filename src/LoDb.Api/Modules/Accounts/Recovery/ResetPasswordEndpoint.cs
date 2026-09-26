using System.Globalization;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Links;
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
internal sealed class ResetPasswordEndpoint(UserManager<User> users, AccountAudit audit)
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
        var token = EmailTokens.Decode(request.Token);
        var user = token is null
            ? null
            : await users.FindByIdAsync(request.UserId.ToString(CultureInfo.InvariantCulture));
        if (user is null || !await IsValidAsync(user, token!))
        {
            return AccountProblem.InvalidToken();
        }

        if (WeakPassword(request.Password) is { } weak)
        {
            return weak;
        }

        var reset = await users.ResetPasswordAsync(user, token!, request.Password!);
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

    private Task<bool> IsValidAsync(User user, string token) =>
        users.VerifyUserTokenAsync(
            user,
            users.Options.Tokens.PasswordResetTokenProvider,
            UserManager<User>.ResetPasswordTokenPurpose,
            token);
}
