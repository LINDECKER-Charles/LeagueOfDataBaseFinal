namespace LoDb.Api.Modules.ClientPolicy.Publishing;

/// <summary>Codes of the invalid fields of a publication, by field.</summary>
internal static class PolicyErrors
{
    /// <summary>Not three numbers, such as 1.2 or v1.2.3.</summary>
    public const string InvalidVersion = "invalid-version";

    /// <summary>A minimum above the latest release: every app would wait for a ghost.</summary>
    public const string AboveLatest = "above-latest";

    /// <summary>A bundle for the desktop app, which Velopack updates whole.</summary>
    public const string BundleNotSupported = "bundle-not-supported";

    public const string InvalidId = "invalid-id";

    /// <summary>Not an absolute https URL, or too long.</summary>
    public const string InvalidUrl = "invalid-url";

    /// <summary>Not 64 lowercase hexadecimal digits.</summary>
    public const string InvalidChecksum = "invalid-checksum";

    /// <summary>Not base64, or too long.</summary>
    public const string InvalidSignature = "invalid-signature";

    /// <summary>A path naming no app.</summary>
    public const string UnknownPlatform = "unknown-platform";
}
