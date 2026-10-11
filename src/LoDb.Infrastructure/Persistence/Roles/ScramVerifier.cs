using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LoDb.Infrastructure.Persistence.Roles;

/// <summary>
/// The SCRAM-SHA-256 verifier PostgreSQL stores for a password (<c>pg_authid.rolpassword</c>):
/// <c>SCRAM-SHA-256$iterations:salt$StoredKey:ServerKey</c>, as RFC 5802 derives it.
/// </summary>
/// <remarks>
/// <c>migrate</c> sets the API's password as a verifier computed here, so the password itself
/// never travels to the server nor appears in a statement a log could keep. It compares the
/// stored verifier first, and changes nothing when the password is already the configured
/// one. The password is printable ASCII (<see cref="AppRoleOptionsValidator"/>), which the
/// server's SASLprep leaves unchanged.
/// </remarks>
internal static class ScramVerifier
{
    private const string Mechanism = "SCRAM-SHA-256";
    private const char PartSeparator = '$';
    private const char ValueSeparator = ':';
    private const int Parts = 3;
    private const int CostParts = 2;
    private const int SaltLength = 16;

    // PostgreSQL's default scram_iterations.
    private const int Iterations = 4096;

    private static readonly byte[] ClientKeyLabel = "Client Key"u8.ToArray();
    private static readonly byte[] ServerKeyLabel = "Server Key"u8.ToArray();

    /// <summary>A verifier of <paramref name="password"/> with a fresh random salt.</summary>
    public static string Create(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return Format(password, RandomNumberGenerator.GetBytes(SaltLength), Iterations);
    }

    /// <summary>
    /// Whether <paramref name="verifier"/> is a SCRAM-SHA-256 verifier of
    /// <paramref name="password"/> at the iteration count <see cref="Create"/> uses; false for
    /// none, an MD5 hash, a malformed value or another count.
    /// </summary>
    /// <remarks>
    /// Another count is never derived: whoever wrote the verifier chose it, the API's role
    /// included (a role may set its own password), and it would set the cost of this
    /// comparison. Such a verifier is rewritten instead, at the cost of one statement.
    /// </remarks>
    public static bool Matches(string password, string? verifier)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (!TryParse(verifier, out var iterations, out var salt))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Format(password, salt, iterations)),
            Encoding.ASCII.GetBytes(verifier));
    }

    private static string Format(string password, byte[] salt, int iterations)
    {
        var salted = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            SHA256.HashSizeInBytes);
        var storedKey = SHA256.HashData(HMACSHA256.HashData(salted, ClientKeyLabel));
        var serverKey = HMACSHA256.HashData(salted, ServerKeyLabel);
        var cost = string.Join(
            ValueSeparator,
            iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt));
        var keys = string.Join(
            ValueSeparator,
            Convert.ToBase64String(storedKey),
            Convert.ToBase64String(serverKey));
        return string.Join(PartSeparator, Mechanism, cost, keys);
    }

    private static bool TryParse(
        [NotNullWhen(true)] string? verifier,
        out int iterations,
        out byte[] salt)
    {
        iterations = 0;
        salt = [];
        var parts = verifier?.Split(PartSeparator);
        if (parts is not { Length: Parts } || parts[0] != Mechanism)
        {
            return false;
        }

        var cost = parts[1].Split(ValueSeparator);
        return cost.Length == CostParts
            && int.TryParse(
                cost[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out iterations)
            && iterations == Iterations
            && TryDecode(cost[1], out salt);
    }

    private static bool TryDecode(string base64, out byte[] bytes)
    {
        bytes = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, bytes, out var written))
        {
            return false;
        }

        bytes = bytes[..written];
        return written > 0;
    }
}
