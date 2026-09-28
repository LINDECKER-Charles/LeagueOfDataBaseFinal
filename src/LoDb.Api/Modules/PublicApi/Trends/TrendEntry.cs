using System.Text.Json.Serialization;

namespace LoDb.Api.Modules.PublicApi.Trends;

/// <summary>An entity of a ranking of <c>/v1/trends</c>.</summary>
/// <param name="Rank">From 1.</param>
/// <param name="Id">The id of the entity in Data Dragon.</param>
/// <param name="Name">Its name in the latest catalog in en_US; left out when unknown.</param>
/// <param name="Edition">
/// <c>modern</c> or <c>classic</c> for the items and the summoner spells; left out for the
/// others.
/// </param>
/// <param name="Views">Its views over the window.</param>
internal sealed record TrendEntry(
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Name,
    [property: JsonPropertyName("edition")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Edition,
    [property: JsonPropertyName("views")] long Views);
