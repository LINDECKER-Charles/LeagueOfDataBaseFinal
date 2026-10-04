using System.Text.Json.Serialization;

namespace LoDb.Api.Modules.PublicApi.Builds;

/// <summary>The body of <c>GET /v1/champions/{championId}/builds</c>.</summary>
/// <param name="ChampionId">The champion as the path spells it.</param>
/// <param name="Data">The page, newest first.</param>
/// <param name="Pagination">Where the page stands.</param>
internal sealed record BuildsResponse(
    [property: JsonPropertyName("champion_id")] string ChampionId,
    [property: JsonPropertyName("data")] IReadOnlyList<BuildItem> Data,
    [property: JsonPropertyName("pagination")] PaginationMeta Pagination);
