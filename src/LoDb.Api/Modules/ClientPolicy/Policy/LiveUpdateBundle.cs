namespace LoDb.Api.Modules.ClientPolicy.Policy;

/// <summary>
/// The live update bundle the Android app must run (ADR 0008): withdrawing a faulty one is
/// publishing the policy again with another bundle, or with none.
/// </summary>
internal sealed record LiveUpdateBundle
{
    /// <summary>The bundle's id, as the live update plugin names it.</summary>
    public required string Id { get; init; }

    /// <summary>Where the app downloads the zip of the shell build.</summary>
    public required string Url { get; init; }

    /// <summary>SHA-256 of the zip, in lowercase hexadecimal.</summary>
    public required string Checksum { get; init; }

    /// <summary>RSA signature of the zip, in base64, checked by the app before use.</summary>
    public required string Signature { get; init; }

    /// <summary>Oldest native shell the bundle runs on.</summary>
    public required string MinimumNativeVersion { get; init; }
}
