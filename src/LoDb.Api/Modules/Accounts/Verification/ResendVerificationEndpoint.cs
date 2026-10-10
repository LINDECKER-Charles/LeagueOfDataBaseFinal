using LoDb.Api.Hosting;
using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Links;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Accounts.Verification;

/// <summary>
/// <c>POST /api/account/verify-email/resend</c>: sends the verification e-mail of the
/// signed-in account again, a few times in all, then once a day
/// (<see cref="VerificationResendOptions"/>).
/// </summary>
/// <remarks>
/// Limited by account, never by address: the request needs a session (ADR 0009).
/// </remarks>
internal sealed class ResendVerificationEndpoint(
    UserManager<User> users,
    MailThrottle throttle,
    AccountMail mail,
    LoDbDbContext db,
    IOptions<VerificationResendOptions> options)
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

        if (await throttle.RemainingWaitAsync(user.Id, Rule, cancellationToken) is { } wait)
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
            await throttle.RecordAsync(user.Id, Rule, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private MailThrottleRule Rule => MailThrottleRule.ConfirmEmail(options.Value);
}
