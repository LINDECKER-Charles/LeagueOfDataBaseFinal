using System.Text.Json.Serialization;

namespace LoDb.Api.Modules.PublicApi.Profiles;

/// <summary>The favorites of a public profile, each null when not chosen.</summary>
internal sealed record ProfileFavorites(
    [property: JsonPropertyName("champion_id")] string? ChampionId,
    [property: JsonPropertyName("item_id")] string? ItemId,
    [property: JsonPropertyName("rune_id")] string? RuneId,
    [property: JsonPropertyName("summoner_id")] string? SummonerId);
