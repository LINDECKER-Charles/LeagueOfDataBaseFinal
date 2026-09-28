using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace LoDb.Api.Tests.Accounts.Support;

/// <summary>
/// What an authenticator app shows for a key in base32, as Identity hands it out: RFC 6238,
/// HMAC-SHA1 over 30-second steps, 6 digits.
/// </summary>
public static class Totp
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const char Padding = '=';
    private const int BitsPerCharacter = 5;
    private const int BitsPerByte = 8;
    private const int StepSeconds = 30;
    private const int Modulus = 1_000_000;
    private const string Format = "D6";
    private const int OffsetMask = 0x0F;
    private const int SignMask = 0x7FFF_FFFF;

    /// <summary>The code of now, by the real clock, which Identity's check reads.</summary>
    public static string Code(string key) => Code(key, TimeProvider.System.GetUtcNow());

    /// <summary>A code of the right form that the key does not give now.</summary>
    public static string WrongCode(string key)
    {
        var code = int.Parse(Code(key), CultureInfo.InvariantCulture);
        return ((code + (Modulus / 2)) % Modulus).ToString(Format, CultureInfo.InvariantCulture);
    }

    private static string Code(string key, DateTimeOffset instant)
    {
        Span<byte> counter = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(
            counter,
            (ulong)(instant.ToUnixTimeSeconds() / StepSeconds));

        // The algorithm of RFC 6238 and of every authenticator app, not a choice of ours.
#pragma warning disable CA5350
        var hash = HMACSHA1.HashData(FromBase32(key), counter);
#pragma warning restore CA5350
        var offset = hash[^1] & OffsetMask;
        var binary = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & SignMask;
        return (binary % Modulus).ToString(Format, CultureInfo.InvariantCulture);
    }

    private static byte[] FromBase32(string text)
    {
        var bytes = new List<byte>(text.Length * BitsPerCharacter / BitsPerByte);
        var buffer = 0;
        var bits = 0;
        foreach (var character in text.TrimEnd(Padding))
        {
            buffer = (buffer << BitsPerCharacter)
                | Base32Alphabet.IndexOf(char.ToUpperInvariant(character));
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
