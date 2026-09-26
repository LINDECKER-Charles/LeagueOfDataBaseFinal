using LoDb.Api.Modules.Accounts.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Admin.Mfa;

/// <summary>
/// <c>POST /api/admin/mfa/enrollment</c>: draws the authenticator key of an administrator
/// who has none yet, with the URI of its QR code.
/// </summary>
internal static class MfaEnrollmentEndpoint
{
    public static void Map(IEndpointRouteBuilder mfa) =>
        mfa.MapPost("/enrollment", StartAsync)
            .WithName("startAdminMfaEnrollment")
            .WithSummary("Draws a new authenticator key for an administrator without one.")
            .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<Ok<MfaSetup>, AccountProblem>> StartAsync(
        [FromServices] MfaEnrollment enrollment,
        HttpContext context)
    {
        var (setup, problem) = await enrollment.StartAsync(context.User);
        return setup is null ? problem! : TypedResults.Ok(setup);
    }
}
