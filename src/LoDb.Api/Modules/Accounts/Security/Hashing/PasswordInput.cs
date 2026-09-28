using System.Text;

namespace LoDb.Api.Modules.Accounts.Security.Hashing;

/// <summary>A password as PHP receives it: its UTF-8 bytes, within Symfony's bounds.</summary>
internal static class PasswordInput
{
    /// <summary>
    /// Symfony's cap on a password, in bytes: it refuses to verify a longer one, so none is
    /// ever hashed here, and a rewritten hash stays readable after a rollback.
    /// </summary>
    public const int MaxBytes = 4096;

    /// <summary>The bytes to hash, or null for an empty or too long password.</summary>
    public static byte[]? Bytes(string? password) =>
        string.IsNullOrEmpty(password) || !FitsSymfony(password)
            ? null
            : Encoding.UTF8.GetBytes(password);

    public static bool FitsSymfony(string password) =>
        Encoding.UTF8.GetByteCount(password) <= MaxBytes;
}
