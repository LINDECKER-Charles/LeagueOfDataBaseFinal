using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoDb.Api.Modules.PublicApi.Builds;

/// <summary>A public build, as <c>/v1</c> lists it.</summary>
/// <param name="Name">Its title.</param>
/// <param name="Description">Its text as written, markup included; null when none.</param>
/// <param name="GameVersion">The patch it was written for.</param>
/// <param name="Runes">The runes document, as stored.</param>
/// <param name="Steps">The item steps, as stored.</param>
/// <param name="ShareUrl">Its page on the site, absolute.</param>
/// <param name="CreatedAt">Its creation, in UTC.</param>
internal sealed record BuildItem(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("runes")] JsonElement Runes,
    [property: JsonPropertyName("steps")] JsonElement Steps,
    [property: JsonPropertyName("share_url")] string ShareUrl,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt);
