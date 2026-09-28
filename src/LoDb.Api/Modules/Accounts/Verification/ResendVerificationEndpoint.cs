using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Links;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Accounts.Verification;

/// <summary>
/// <c>POST /api/account/verify-email/resend</c>: sends the verification e-mail of the
/// signed-in account again, 3 times per 15 minutes at most.
/// </summary>
internal sealed class ResendVerificationEndpoint(
    UserManager<User> users,
    MailThrottle throttle,
    AccountMail mail,
    LoDbDbContext db)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/verify-email/resend",
                static (
                    [FromBody] ResendVerificationRequest? request,
                    [FromServices] ResendVerificationEndpoint endpoint,
                    HttpContext context) => endpoint.ResendAsync(request, context))
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .WithName("resendVerificationEmail")
            .WithSummary("Sends the verification e-mail of the signed-in account again.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    /// <param name="request">Optional: a front without a locale to give sends no body.</param>
    /// <param name="context">The request of the signed-in account.</param>
    public async Task<Results<Accepted, AccountProblem>> ResendAsync(
        ResendVerificationRequest? request,
        HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        // A banned account whose session is not revalidated yet reads as signed out, as in /me.
        if (await users.GetUserAsync(context.User) is not { IsBanned: false } user)
        {
            return AccountProblem.AuthenticationRequired();
        }

        if (user.EmailConfirmed)
        {
            return AccountProblem.EmailAlreadyVerified();
        }

        var rule = MailThrottleRule.ConfirmEmail;
        if (await throttle.RemainingWaitAsync(user.Id, rule, cancellationToken) is { } wait)
        {
            return AccountProblem.ResendThrottled(wait);
        }

        await SendAsync(user, request?.Locale ?? UiLocales.Fallback, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    // Only a queued e-mail counts toward the limit.
    private async Task SendAsync(User user, UiLocale locale, CancellationToken cancellationToken)
    {
        if (await mail.SendConfirmationAsync(user, locale, cancellationToken))
        {
            await throttle.RecordAsync(user.Id, MailThrottleRule.ConfirmEmail, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
