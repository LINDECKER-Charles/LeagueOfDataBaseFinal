using System.Text.Json.Serialization;

namespace LoDb.Api.Modules.PublicApi.Http;

/// <summary>A refusal: a stable machine-readable code and a message for humans.</summary>
internal sealed record ErrorBody(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);
