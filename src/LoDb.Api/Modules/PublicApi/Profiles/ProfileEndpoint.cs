using LoDb.Api.Modules.PublicApi.Gate;
using LoDb.Api.Modules.PublicApi.Http;
using LoDb.Api.Modules.PublicApi.OpenApi;
using LoDb.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoDb.Api.Modules.PublicApi.Profiles;

/// <summary>
/// <c>GET /v1/profiles/{username}</c>: a public profile, found whatever the case of the
/// username.
/// </summary>
/// <remarks>
/// A private, banned or unknown account gets the same 404: the answer never tells whether
/// an account exists. go-api served the banned ones.
/// </remarks>
internal sealed class ProfileEndpoint(LoDbDbContext db)
{
    public const string Pattern = "/profiles/{username}";

    private static readonly V1Error NoPublicProfile = new(
        StatusCodes.Status404NotFound,
        V1Errors.NotFound,
        "no public profile for this username");

    public static void Map(IEndpointRouteBuilder v1) =>
        v1.MapRead(
                Pattern,
                static (
                    string username,
                    [FromServices] ProfileEndpoint endpoint,
                    CancellationToken cancellationToken) =>
                    endpoint.GetAsync(username, cancellationToken))
            .WithName("v1GetProfile")
            .WithSummary("A public profile: its favorites and how many public builds it has.")
            .ProducesV1<ProfileResponse>()
            .ProducesV1Refusals(StatusCodes.Status404NotFound);

    public async Task<IResult> GetAsync(string username, CancellationToken cancellationToken)
    {
        // LOWER() on both sides, as go-api reads it: the unique index of the usernames.
        var profile = await db.Users
            .FromSql($"SELECT * FROM users WHERE LOWER(username) = LOWER({username})")
            .AsNoTracking()
            // One row at most; ordered all the same, so that EF knows which one is read.
            .OrderBy(user => user.Id)
            .Select(user => new
            {
                user.UserName,
                user.CreatedAt,
                user.IsPublicProfile,
                user.IsBanned,
                user.FavoriteChampionId,
                user.FavoriteItemId,
                user.FavoriteRuneId,
                user.FavoriteSummonerId,
                PublicBuilds = db.Builds.LongCount(
                    build => build.OwnerId == user.Id && build.IsPublic),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (profile is null || !profile.IsPublicProfile || profile.IsBanned)
        {
            return NoPublicProfile;
        }

        return V1Json.Ok(new ProfileResponse(
            profile.UserName!,
            profile.CreatedAt.UtcDateTime,
            new ProfileFavorites(
                profile.FavoriteChampionId,
                profile.FavoriteItemId,
                profile.FavoriteRuneId,
                profile.FavoriteSummonerId),
            profile.PublicBuilds));
    }
}
