using System.Text.Json.Serialization;

namespace LoDb.Api.Modules.PublicApi.Profiles;

/// <summary>The body of <c>GET /v1/profiles/{username}</c>.</summary>
/// <param name="Username">The username as the account spells it.</param>
/// <param name="CreatedAt">Creation of the account, in UTC.</param>
/// <param name="Favorites">The favorites shown on the profile.</param>
/// <param name="PublicBuilds">Builds of the account anyone can open.</param>
internal sealed record ProfileResponse(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("favorites")] ProfileFavorites Favorites,
    [property: JsonPropertyName("public_builds")] long PublicBuilds);
