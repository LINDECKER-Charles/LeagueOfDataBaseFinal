using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using BCryptHashes = BCrypt.Net.BCrypt;

namespace LoDb.Api.Modules.Accounts.Security.Hashing;

/// <summary>The legacy bcrypt hashes: <c>$2y$</c>, <c>$2a$</c> and <c>$2b$</c>.</summary>
internal static class LegacyBcrypt
{
    /// <summary>
    /// The highest cost verified: the legacy stack's own (13). Each step doubles the work, so
    /// a stored hash asking for more is refused rather than allowed to hold a core for seconds.
    /// </summary>
    public const int MaxCost = 13;

    private const string Prefix = "$2";

    // "$2y$13$…": the two digits of the cost follow the version.
    private const int CostStart = 4;
    private const int CostDigits = 2;

    // bcrypt reads 72 bytes at most and stops at a NUL: Symfony hashes such a password with
    // SHA-512 first, then passes its base64 to bcrypt, and so must the check.
    private const int MaxBcryptBytes = 72;

    public static bool IsBcrypt(string hash) => hash.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// Whether <paramref name="password"/> matches; false for a malformed hash or one whose
    /// cost is above <see cref="MaxCost"/>, which is never computed.
    /// </summary>
    public static bool Verify(string hash, string password)
    {
        if (!IsWithinCost(hash))
        {
            return false;
        }

        try
        {
            return BCryptHashes.Verify(Input(password), hash);
        }
        catch (Exception exception) when (exception is SaltParseException or ArgumentException)
        {
            return false;
        }
    }

    private static bool IsWithinCost(string hash) =>
        hash.Length >= CostStart + CostDigits
        && int.TryParse(
            hash.AsSpan(CostStart, CostDigits),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var cost)
        && cost <= MaxCost;

    private static string Input(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        return bytes.Length > MaxBcryptBytes || password.Contains('\0', StringComparison.Ordinal)
            ? Convert.ToBase64String(SHA512.HashData(bytes))
            : password;
    }
}
