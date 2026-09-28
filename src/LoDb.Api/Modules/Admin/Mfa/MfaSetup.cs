namespace LoDb.Api.Modules.Admin.Mfa;

/// <summary>A new authenticator key, to scan or type before confirming it with a code.</summary>
internal sealed record MfaSetup
{
    /// <summary>The shared key, in base32, for an app that cannot scan.</summary>
    public required string SharedKey { get; init; }

    /// <summary>The <c>otpauth://</c> URI the QR code carries.</summary>
    public required string AuthenticatorUri { get; init; }
}
