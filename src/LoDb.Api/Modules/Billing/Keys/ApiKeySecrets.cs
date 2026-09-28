using System.Security.Cryptography;
using System.Text;

namespace LoDb.Api.Modules.Billing.Keys;

/// <summary>
/// The secret of an API key as the legacy <c>ApiKeyIssuer</c> makes it: <c>lodb_</c> and 40
/// lowercase hexadecimal digits, stored as its SHA-256 and shown by its first 12 characters.
/// </summary>
internal static class ApiKeySecrets
{
    public const string Prefix = "lodb_";

    /// <summary>Name of a key its owner did not name.</summary>
    public const string DefaultName = "default";

    // 20 bytes make the 40 digits the public API checks.
    private const int SecretBytes = 20;
    private const int DisplayLength = 12;

    public static string Generate() =>
        Prefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(SecretBytes));

    /// <summary>What <c>api_keys.key_hash</c> stores of <paramref name="secret"/>.</summary>
    public static string Hash(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary>What <c>api_keys.key_prefix</c> shows of <paramref name="secret"/>.</summary>
    public static string DisplayPrefix(string secret) => secret[..DisplayLength];
}
