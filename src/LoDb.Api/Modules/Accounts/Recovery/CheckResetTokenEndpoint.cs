using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Accounts.Recovery;

/// <summary>
/// <c>POST /api/account/reset-password/check</c>: whether the link of a reset e-mail still
/// sets a password, without using it.
/// </summary>
/// <remarks>
/// The reset page asks it when it opens, so that a damaged, expired or used link goes back to
/// the request of another one before any password is typed, as the legacy page did. A POST
/// keeps the token out of the URLs that proxies log.
/// </remarks>
internal sealed class CheckResetTokenEndpoint(ResetLinks links)
{
    public static void Map(IEndpointRouteBuilder account) =>
        account.MapPost(
                "/reset-password/check",
                static (
                    [FromBody] CheckResetTokenRequest request,
                    [FromServices] CheckResetTokenEndpoint endpoint) =>
                    endpoint.CheckAsync(request))
            .RequireRateLimiting(ResetCheckRateLimit.Policy)
            .WithName("checkPasswordResetToken")
            .WithSummary("Tells whether the link sent by e-mail still sets a password.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    public async Task<Results<NoContent, AccountProblem>> CheckAsync(
        CheckResetTokenRequest request) =>
        await links.ReadAsync(request.UserId, request.Token) is null
            ? AccountProblem.InvalidToken()
            : TypedResults.NoContent();
}
