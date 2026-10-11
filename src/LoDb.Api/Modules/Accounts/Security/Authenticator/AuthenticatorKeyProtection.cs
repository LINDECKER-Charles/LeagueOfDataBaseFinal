using Microsoft.AspNetCore.DataProtection;

namespace LoDb.Api.Modules.Accounts.Security.Authenticator;

/// <summary>
/// The authenticator key at rest: sealed by Data Protection, whose keys the certificate of
/// the host encrypts, so that a copy of the database alone gives no second factor.
/// </summary>
/// <remarks>
/// A sealed key is marked by its prefix. A key without it was written in clear by an earlier
/// version: it is read as it is until <see cref="AuthenticatorSecretsUpkeep"/> seals it, so
/// that no administrator loses their authenticator in between.
/// </remarks>
internal sealed class AuthenticatorKeyProtection(IDataProtectionProvider protection)
{
    /// <summary>
    /// Purpose of the protector: a key ring shared with another use could not open it, and
    /// "v1" lets a future format move on without reading this one wrongly.
    /// </summary>
    public const string Purpose = "LoDb.Accounts.AuthenticatorKey.v1";

    /// <summary>The mark of a sealed key.</summary>
    public const string SealedPrefix = "dp1:";

    private readonly IDataProtector _protector = protection.CreateProtector(Purpose);

    public static bool IsSealed(string? stored) =>
        stored?.StartsWith(SealedPrefix, StringComparison.Ordinal) == true;

    public string Seal(string key) => SealedPrefix + _protector.Protect(key);

    /// <summary>The key of <paramref name="stored"/>, sealed or still in clear.</summary>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The key ring that sealed it is gone: <c>admin mfa-reset</c> then draws a new key.
    /// </exception>
    public string? Reveal(string? stored) =>
        IsSealed(stored) ? _protector.Unprotect(stored![SealedPrefix.Length..]) : stored;
}
