using LoDb.Infrastructure.Persistence.PublicApi;

namespace LoDb.Api.Modules.PublicApi.Keys;

/// <summary>A key just stored, with the secret whose fingerprint it holds.</summary>
/// <param name="Key">The row, as committed.</param>
/// <param name="Secret">The only copy of the secret: nothing stores it.</param>
internal sealed record KeyIssue(ApiKey Key, string Secret);
