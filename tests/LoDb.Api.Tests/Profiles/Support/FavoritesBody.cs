namespace LoDb.Api.Tests.Profiles.Support;

/// <summary>The body of <c>PUT /api/profile/favorites</c>: every slot, null when empty.</summary>
public sealed record FavoritesBody(
    string? Champion = null,
    string? Item = null,
    string? Rune = null,
    string? Summoner = null,
    string? Skin = null);
