using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Mfa;

/// <summary>
/// <c>POST /api/admin/mfa/confirm</c>: turns the second factor on with a code of the key
/// drawn by the enrollment, and answers the recovery codes and the session, now opened
/// with the second factor.
/// </summary>
internal static class MfaConfirmEndpoint
{
    public static void Map(IEndpointRouteBuilder mfa) =>
        mfa.MapPost("/confirm", ConfirmAsync)
            .WithName("confirmAdminMfa")
            .WithSummary("Turns the authenticator on with one of its codes.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<Results<Ok<MfaConfirmation>, AccountProblem>> ConfirmAsync(
        [FromBody] MfaConfirmRequest request,
        [FromServices] MfaEnrollment enrollment,
        HttpContext context)
    {
        var (done, problem) = await enrollment.ConfirmAsync(context, request?.Code);
        return done is null ? problem! : TypedResults.Ok(done);
    }
}
