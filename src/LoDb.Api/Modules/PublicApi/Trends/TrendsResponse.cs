using System.Text.Json.Serialization;

namespace LoDb.Api.Modules.PublicApi.Trends;

/// <summary>The body of <c>GET /v1/trends/{type}</c>.</summary>
/// <param name="Type">The type as the path spells it.</param>
/// <param name="Range">The window, <c>7d</c> or <c>30d</c>.</param>
/// <param name="Entries">The most viewed entities, 25 at most.</param>
internal sealed record TrendsResponse(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("range")] string Range,
    [property: JsonPropertyName("entries")] IReadOnlyList<TrendEntry> Entries);
