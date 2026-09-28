using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Accounts.Registration;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Api.Modules.Profiles.Reading;
using LoDb.Infrastructure.Persistence.Accounts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Profiles.Cards;

/// <summary>
/// <c>GET /api/profiles/{username}</c>: the public card of a profile, on the patch it pins.
/// </summary>
/// <remarks>
/// Opt-in: a name that is no username, an unknown account, a private profile and a banned
/// owner all get the same 404, found before anything else is read, so the route tells
/// nothing about which accounts exist.
/// </remarks>
internal sealed class PublicProfileEndpoint(
    UserManager<User> users,
    ProfileCatalog catalogs,
    PublicCards cards)
{
    public static void Map(IEndpointRouteBuilder profiles) =>
        profiles.MapGet(
                "/{username}",
                static (
                    [FromRoute] string username,
                    [AsParameters] ProfileQuery query,
                    [FromServices] PublicProfileEndpoint endpoint,
                    CancellationToken aborted) => endpoint.GetAsync(username, query, aborted))
            .WithName("getPublicProfile")
            .WithSummary(
                "The public card of a profile; one 404 for an unknown, private or banned one.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    public async Task<Results<Ok<PublicProfile>, AccountProblem, CatalogProblem>> GetAsync(
        string username,
        ProfileQuery query,
        CancellationToken cancellationToken)
    {
        if (await FindPublicAsync(username) is not { } user)
        {
            return ProfileProblems.NotFound();
        }

        var read = await catalogs.OpenAsync(
            user.PreferredVersion,
            query.Browsing,
            cancellationToken);
        if (ProfileCatalog.IsClientError(read, out var problem))
        {
            return problem;
        }

        return TypedResults.Ok(await cards.BuildAsync(user, read.Context, cancellationToken));
    }

    // Whatever the case, as the unique index of the usernames compares.
    private async Task<User?> FindPublicAsync(string username)
    {
        if (!RegistrationRules.IsUsername(username))
        {
            return null;
        }

        var user = await users.FindByNameAsync(username);
        return user is { IsPublicProfile: true, IsBanned: false } ? user : null;
    }
}
