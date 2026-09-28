using System.Text.Json.Serialization;

namespace LoDb.Api.Modules.PublicApi.Http;

/// <summary>
/// The body of every JSON refusal of <c>/v1</c>, <c>{"error": {"code", "message"}}</c>: a
/// contract of go-api, never a ProblemDetails.
/// </summary>
internal sealed record ErrorEnvelope([property: JsonPropertyName("error")] ErrorBody Error);
