using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using BCryptHashes = BCrypt.Net.BCrypt;

namespace LoDb.Api.Modules.Accounts.Security.Hashing;

/// <summary>The legacy bcrypt hashes: <c>$2y$</c>, <c>$2a$</c> and <c>$2b$</c>.</summary>
internal static class LegacyBcrypt
{
    private const string Prefix = "$2";

    // bcrypt reads 72 bytes at most and stops at a NUL: Symfony hashes such a password with
    // SHA-512 first, then passes its base64 to bcrypt, and so must the check.
    private const int MaxBcryptBytes = 72;

    public static bool IsBcrypt(string hash) => hash.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="password"/> matches; false for a malformed hash.</summary>
    public static bool Verify(string hash, string password)
    {
        try
        {
            return BCryptHashes.Verify(Input(password), hash);
        }
        catch (Exception exception) when (exception is SaltParseException or ArgumentException)
        {
            return false;
        }
    }

    private static string Input(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        return bytes.Length > MaxBcryptBytes || password.Contains('\0', StringComparison.Ordinal)
            ? Convert.ToBase64String(SHA512.HashData(bytes))
            : password;
    }
}
