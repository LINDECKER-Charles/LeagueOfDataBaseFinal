using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Profiles.Curation;

/// <summary><c>PUT /api/profile/visibility</c>: shows or hides the public card.</summary>
internal sealed class VisibilityEndpoint(ProfileOwners owners, LoDbDbContext db, ProfileAudit audit)
{
    public static void Map(IEndpointRouteBuilder profile) =>
        profile.MapPut(
                "/visibility",
                static (
                    [FromBody] VisibilityRequest request,
                    [FromServices] VisibilityEndpoint endpoint,
                    HttpContext context) => endpoint.SetAsync(request, context))
            .WithName("setProfileVisibility")
            .WithSummary("Shows the public card to visitors, or hides it behind a 404.");

    public async Task<Results<NoContent, AccountProblem>> SetAsync(
        VisibilityRequest request,
        HttpContext context)
    {
        if (await owners.FindAsync(context.User) is not { } owner)
        {
            return ProfileProblems.SignedOut();
        }

        owner.IsPublicProfile = request.IsPublic;
        await db.SaveChangesAsync(context.RequestAborted);
        await audit.UpdatedAsync(ProfileAudit.VisibilitySection, context.RequestAborted);
        return TypedResults.NoContent();
    }
}
