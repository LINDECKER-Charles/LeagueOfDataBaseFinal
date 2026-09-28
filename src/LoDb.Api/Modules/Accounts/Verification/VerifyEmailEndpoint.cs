using System.Globalization;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Links;
using LoDb.Infrastructure.Audit;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Accounts.Verification;

/// <summary>
/// <c>POST /api/account/verify-email</c>: verifies the e-mail of the account a link names,
/// signed in or not, from any device.
/// </summary>
internal sealed class VerifyEmailEndpoint(UserManager<User> users, AccountAudit audit)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/verify-email",
                static (
                    [FromBody] VerifyEmailRequest request,
                    [FromServices] VerifyEmailEndpoint endpoint,
                    CancellationToken aborted) => endpoint.VerifyAsync(request, aborted))
            .WithName("verifyEmail")
            .WithSummary("Verifies an e-mail with the parameters of the link sent to it.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    public async Task<Results<Ok<EmailVerification>, AccountProblem>> VerifyAsync(
        VerifyEmailRequest request,
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

        if (user.EmailConfirmed)
        {
            return TypedResults.Ok(new EmailVerification { AlreadyVerified = true });
        }

        if (!(await users.ConfirmEmailAsync(user, token!)).Succeeded)
        {
            return AccountProblem.InvalidToken();
        }

        await audit.RecordAsync(AuditAction.UserEmailVerified, user, cancellationToken);
        return TypedResults.Ok(new EmailVerification { AlreadyVerified = false });
    }

    // Checked first, so that a verified account tells nothing to whoever lacks its link.
    private Task<bool> IsValidAsync(User user, string token) =>
        users.VerifyUserTokenAsync(
            user,
            users.Options.Tokens.EmailConfirmationTokenProvider,
            UserManager<User>.ConfirmEmailTokenPurpose,
            token);
}
