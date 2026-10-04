namespace LoDb.Api.Modules.ClientPolicy.Publishing;

/// <summary>The live update bundle of a publication; every field is required.</summary>
internal sealed record BundleRequest
{
    /// <summary>The id the live update plugin knows it by: letters, digits, . _ -.</summary>
    public string? Id { get; init; }

    /// <summary>Absolute https URL of the zip, such as a GitHub Release asset.</summary>
    public string? Url { get; init; }

    /// <summary>SHA-256 of the zip, 64 lowercase hexadecimal digits.</summary>
    public string? Checksum { get; init; }

    /// <summary>RSA signature of the zip, in base64.</summary>
    public string? Signature { get; init; }

    /// <summary>Oldest native shell the bundle runs on, such as 1.3.0.</summary>
    public string? MinimumNativeVersion { get; init; }
}
