using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Catalog.Http;
using LoDb.Api.Modules.Catalog.Reading;
using LoDb.Api.Modules.Profiles.Favorites;
using LoDb.Api.Modules.Profiles.Http;
using LoDb.Api.Modules.Profiles.Reading;
using LoDb.Domain.Languages;
using LoDb.Infrastructure.Persistence;
using LoDb.Infrastructure.Persistence.Accounts;
using LoDb.Ingestion.Catalog.Snapshots;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LoDb.Api.Modules.Profiles.Curation;

/// <summary>
/// <c>PUT /api/profile/favorites</c>: the four favorites and the skin, checked against the
/// version the profile pins, as the page shows them.
/// </summary>
/// <remarks>
/// A favorite that version lacks is kept when it is the stored one, so it is never wiped.
/// A catalog that cannot be read refuses the whole save rather than drop anything. The skin
/// is only checked for its form: its art is hotlinked from the id, and a save must not
/// depend on data it does not need.
/// </remarks>
internal sealed class SaveFavoritesEndpoint(
    ProfileOwners owners,
    ProfileCatalog catalogs,
    LoDbDbContext db,
    ProfileAudit audit)
{
    public static void Map(IEndpointRouteBuilder profile) =>
        profile.MapPut(
                "/favorites",
                static (
                    [FromBody] SaveFavoritesRequest request,
                    [FromQuery(Name = "version")] string? version,
                    [FromServices] SaveFavoritesEndpoint endpoint,
                    HttpContext context) => endpoint.SaveAsync(request, version, context))
            .WithName("saveFavorites")
            .WithSummary("Saves the favorites and the skin, checked on the pinned version.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    /// <param name="request">The slots.</param>
    /// <param name="version">
    /// The version browsed, used when the profile pins none; the latest when unset.
    /// </param>
    /// <param name="context">The request.</param>
    public async Task<Results<Ok<FavoritesSaved>, AccountProblem, CatalogProblem>> SaveAsync(
        SaveFavoritesRequest request,
        string? version,
        HttpContext context)
    {
        if (await owners.FindAsync(context.User) is not { } owner)
        {
            return ProfileProblems.SignedOut();
        }

        var aborted = context.RequestAborted;
        var scope = new CatalogScope(version, DdragonLanguage.EnUs.Code);
        var read = await catalogs.OpenAsync(owner.PreferredVersion, scope, aborted);
        if (!read.IsOpen)
        {
            return read.Problem;
        }

        var saved = Sanitize(request, owner, read.Context.Catalog);
        saved.Favorites.ApplyTo(owner);
        owner.FavoriteSkinId = saved.Skin;
        await db.SaveChangesAsync(aborted);
        await audit.UpdatedAsync(ProfileAudit.FavoritesSection, aborted);
        return TypedResults.Ok(saved);
    }

    private static FavoritesSaved Sanitize(
        SaveFavoritesRequest request,
        User owner,
        CatalogSnapshot catalog)
    {
        var favorites = FavoriteSelection.Sanitize(
            request.Ids,
            FavoriteIds.Of(owner),
            (slot, id) => FavoriteCatalog.Find(catalog, slot, id) is not null);
        var skin = request.Skin?.Trim() ?? string.Empty;
        var isSkinKept = skin.Length > 0 && SkinId.IsWellFormed(skin);
        return new FavoritesSaved
        {
            Favorites = favorites.Values,
            Skin = isSkinKept ? skin : null,
            Rejected = favorites.Rejected,
            IsSkinRejected = skin.Length > 0 && !isSkinKept,
        };
    }
}
