namespace LoDb.Domain.Builds.Metadata;

/// <summary>
/// The token of a build's <c>/b/{token}</c> link: 12 random bytes in lowercase hexadecimal,
/// 24 characters of <c>[a-f0-9]</c>. It is the key of an unlisted build: whoever has it reads
/// the build, public or not.
/// </summary>
public static class ShareTokens
{
    public const int ByteCount = 12;
    public const int Length = ByteCount * 2;

    /// <summary>The route constraint and schema pattern of a token.</summary>
    public const string Pattern = "^[a-f0-9]{24}$";

    /// <summary>The token of <paramref name="randomBytes"/>, drawn by the caller.</summary>
    public static string Format(ReadOnlySpan<byte> randomBytes)
    {
        if (randomBytes.Length != ByteCount)
        {
            throw new ArgumentException(
                $"A share token is made of {ByteCount} bytes.",
                nameof(randomBytes));
        }

        return Convert.ToHexStringLower(randomBytes);
    }

    public static bool IsWellFormed(string? token) =>
        token is { Length: Length } && token.All(IsLowerHexDigit);

    private static bool IsLowerHexDigit(char c) => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f';
}
