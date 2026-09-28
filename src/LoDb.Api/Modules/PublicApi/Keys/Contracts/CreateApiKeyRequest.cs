namespace LoDb.Api.Modules.PublicApi.Keys.Contracts;

/// <summary>Body of <c>POST /api/account/api-key</c>.</summary>
internal sealed record CreateApiKeyRequest
{
    /// <summary>
    /// What the owner calls the key, trimmed and cut to 64 characters; <c>default</c> when
    /// blank.
    /// </summary>
    public string? Name { get; init; }
}
