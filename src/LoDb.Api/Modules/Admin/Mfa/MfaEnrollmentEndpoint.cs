using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Mfa;

/// <summary>
/// <c>POST /api/admin/mfa/enrollment</c>: draws the authenticator key of an administrator
/// who has none yet and holds an invitation, with the URI of its QR code.
/// </summary>
internal static class MfaEnrollmentEndpoint
{
    public static void Map(IEndpointRouteBuilder mfa) =>
        mfa.MapPost("/enrollment", StartAsync)
            .WithName("startAdminMfaEnrollment")
            .WithSummary("Draws a new authenticator key for an invited administrator without one.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Ok<MfaSetup>, AccountProblem>> StartAsync(
        [FromBody] MfaEnrollmentRequest request,
        [FromServices] MfaEnrollment enrollment,
        HttpContext context)
    {
        var (setup, problem) = await enrollment.StartAsync(context.User, request.Invitation);
        return setup is null ? problem! : TypedResults.Ok(setup);
    }
}
