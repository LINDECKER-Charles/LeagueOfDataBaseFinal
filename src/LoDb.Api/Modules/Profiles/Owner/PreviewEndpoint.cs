using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Profiles.Cards;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Api.Modules.Profiles.Reading;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Profiles.Owner;

/// <summary>
/// <c>GET /api/profile/preview</c>: the public card of the signed-in account exactly as
/// visitors see it, even while it is private and they get a 404.
/// </summary>
internal sealed class PreviewEndpoint(
    ProfileOwners owners,
    ProfileCatalog catalogs,
    PublicCards cards)
{
    public static void Map(IEndpointRouteBuilder profile) =>
        profile.MapGet(
                "/preview",
                static (
                    [AsParameters] ProfileQuery query,
                    [FromServices] PreviewEndpoint endpoint,
                    HttpContext context) => endpoint.GetAsync(query, context))
            .WithName("previewOwnProfile")
            .WithSummary("The public card of the signed-in account, shown even while private.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    public async Task<Results<Ok<PublicProfile>, AccountProblem, CatalogProblem>> GetAsync(
        ProfileQuery query,
        HttpContext context)
    {
        if (await owners.FindAsync(context.User) is not { } owner)
        {
            return ProfileProblems.SignedOut();
        }

        var aborted = context.RequestAborted;
        var read = await catalogs.OpenAsync(owner.PreferredVersion, query.Browsing, aborted);
        if (ProfileCatalog.IsClientError(read, out var problem))
        {
            return problem;
        }

        return TypedResults.Ok(await cards.BuildAsync(owner, read.Context, aborted));
    }
}
