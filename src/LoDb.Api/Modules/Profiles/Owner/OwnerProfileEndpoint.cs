using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Profiles.Deletion;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Api.Modules.Profiles.Reading;
using LoDb.Api.Modules.Profiles.Showcase;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Profiles.Owner;

/// <summary>
/// <c>GET /api/profile</c>: the signed-in account's profile, its favorites resolved on the
/// version it pins.
/// </summary>
internal sealed class OwnerProfileEndpoint(ProfileOwners owners, ProfileCatalog catalogs)
{
    public static void Map(IEndpointRouteBuilder profile) =>
        profile.MapGet(
                string.Empty,
                static (
                    [AsParameters] ProfileQuery query,
                    [FromServices] OwnerProfileEndpoint endpoint,
                    HttpContext context) => endpoint.GetAsync(query, context))
            .WithName("getOwnProfile")
            .WithSummary("The signed-in account's profile, favorites on its pinned version.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    public async Task<Results<Ok<OwnerProfile>, AccountProblem, CatalogProblem>> GetAsync(
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

        var showcase = await ProfileShowcase.ResolveAsync(owner, read.Context, aborted);
        return TypedResults.Ok(Describe(owner, showcase));
    }

    private static OwnerProfile Describe(User owner, ProfileShowcase showcase) => new()
    {
        Username = owner.UserName ?? string.Empty,
        RiotTagline = owner.RiotTagline,
        MaskedEmail = EmailMask.Apply(owner.Email),
        MemberSince = owner.CreatedAt,
        HasPassword = owner.PasswordHash is not null,
        DeletionConfirmation = DeletionRules.ConfirmationOf(owner),
        IsPublic = owner.IsPublicProfile,
        PreferredVersion = owner.PreferredVersion,
        Showcase = showcase,
        Backdrop = ProfileBackdrop.OfChampion(owner.FavoriteChampionId),
    };
}
