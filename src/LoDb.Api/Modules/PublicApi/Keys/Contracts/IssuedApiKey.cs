namespace LoDb.Api.Modules.PublicApi.Keys.Contracts;

/// <summary>A key just created or regenerated, with the only copy of its secret.</summary>
internal sealed record IssuedApiKey
{
    /// <summary>
    /// <c>lodb_</c> and 40 hexadecimal digits. Only its fingerprint is stored: no later answer
    /// shows it again.
    /// </summary>
    public required string Secret { get; init; }

    public required ApiKeyOverview Key { get; init; }
}
