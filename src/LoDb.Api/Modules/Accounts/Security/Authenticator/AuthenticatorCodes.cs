using System.Buffers.Binary;
using System.Security.Cryptography;

namespace LoDb.Api.Modules.Accounts.Security.Authenticator;

/// <summary>
/// The codes of an authenticator app (RFC 6238): an HMAC-SHA1 of the key over 30-second
/// steps, read as six digits. The key is the base32 text the enrolment hands out.
/// </summary>
internal static class AuthenticatorCodes
{
    /// <summary>Seconds of one time step: every app shows a new code at this pace.</summary>
    public const int StepSeconds = 30;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const char Padding = '=';
    private const int BitsPerCharacter = 5;
    private const int BitsPerByte = 8;
    private const int Modulus = 1_000_000;
    private const int OffsetMask = 0x0F;
    private const int SignMask = 0x7FFF_FFFF;

    /// <summary>The step <paramref name="instant"/> falls in.</summary>
    public static long StepAt(DateTimeOffset instant) => instant.ToUnixTimeSeconds() / StepSeconds;

    /// <summary>The code the app shows during <paramref name="step"/>.</summary>
    public static int CodeOf(byte[] key, long step)
    {
        ArgumentNullException.ThrowIfNull(key);
        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);

        // SHA-1 is what RFC 6238 and every authenticator app compute, not a choice of ours.
#pragma warning disable CA5350
        var hash = HMACSHA1.HashData(key, counter);
#pragma warning restore CA5350
        var offset = hash[^1] & OffsetMask;
        var binary = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & SignMask;
        return binary % Modulus;
    }

    /// <summary>The bytes of a base32 key, whatever its case; null when it is not base32.</summary>
    public static byte[]? KeyOf(string? base32)
    {
        if (string.IsNullOrWhiteSpace(base32))
        {
            return null;
        }

        var bytes = new List<byte>(base32.Length * BitsPerCharacter / BitsPerByte);
        var buffer = 0;
        var bits = 0;
        foreach (var character in base32.TrimEnd(Padding))
        {
            var value = Base32Alphabet.IndexOf(char.ToUpperInvariant(character));
            if (value < 0)
            {
                return null;
            }

            buffer = (buffer << BitsPerCharacter) | value;
            bits += BitsPerCharacter;
            if (bits >= BitsPerByte)
            {
                bits -= BitsPerByte;
                bytes.Add((byte)(buffer >> bits));
                buffer &= (1 << bits) - 1;
            }
        }

        return [.. bytes];
    }
}
