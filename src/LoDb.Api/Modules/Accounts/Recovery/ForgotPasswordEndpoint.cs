using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Links;
using LoDb.Api.Modules.Accounts.Registration;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Accounts.Recovery;

/// <summary>
/// <c>POST /api/account/forgot-password</c>: sends a link setting a new password to the
/// account of an e-mail, one an hour at most.
/// </summary>
/// <remarks>
/// The answer is the same whether an account uses the e-mail or not, has had its link this
/// hour or is banned: the endpoint tells no one which e-mails have an account.
/// </remarks>
internal sealed class ForgotPasswordEndpoint(
    UserManager<User> users,
    MailThrottle throttle,
    AccountMail mail,
    LoDbDbContext db)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/forgot-password",
                static (
                    [FromBody] ForgotPasswordRequest request,
                    [FromServices] ForgotPasswordEndpoint endpoint,
                    CancellationToken aborted) => endpoint.RequestAsync(request, aborted))
            .RequireRateLimiting(RateLimitingPolicies.PasswordReset)
            .WithName("requestPasswordReset")
            .WithSummary("Sends a link setting a new password, if an account uses the e-mail.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    public async Task<Results<Accepted, AccountProblem>> RequestAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var email = RegistrationRules.Email(request.Email);
        var errors = new FieldErrors();
        RegistrationRules.CheckEmail(errors, email);
        if (!errors.IsEmpty)
        {
            return errors.ToProblem();
        }

        var user = await users.FindByEmailAsync(email);
        var rule = MailThrottleRule.ResetPassword;
        if (user is { IsBanned: false }
            && await throttle.RemainingWaitAsync(user.Id, rule, cancellationToken) is null
            && await mail.SendPasswordResetAsync(
                user,
                request.Locale ?? UiLocales.Fallback,
                cancellationToken))
        {
            await throttle.RecordAsync(user.Id, rule, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Accepted((string?)null);
    }
}
