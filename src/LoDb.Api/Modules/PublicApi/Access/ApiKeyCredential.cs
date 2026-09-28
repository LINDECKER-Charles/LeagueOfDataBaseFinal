using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace LoDb.Api.Modules.PublicApi.Access;

/// <summary>
/// The key a request presents, and its fingerprint: the SHA-256 of the whole key, which is
/// all <c>api_keys</c> stores.
/// </summary>
/// <remarks>
/// Only <c>lodb_</c> followed by 40 lowercase hexadecimal digits is a key: anything else is
/// refused without a database read.
/// </remarks>
internal static class ApiKeyCredential
{
    public const string Prefix = "lodb_";
    public const int SecretLength = 40;
    public const string ApiKeyHeader = "X-Api-Key";

    // Case-sensitive, space included, as go-api compares it.
    private const string BearerPrefix = "Bearer ";

    private static readonly SearchValues<char> LowerHex = SearchValues.Create("0123456789abcdef");

    /// <summary>
    /// The bearer token when <c>Authorization</c> starts with <c>Bearer </c>, otherwise the
    /// <c>X-Api-Key</c> header, trimmed; empty when the request carries none.
    /// </summary>
    public static string Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = First(request.Headers.Authorization);
        return authorization.StartsWith(BearerPrefix, StringComparison.Ordinal)
            ? authorization[BearerPrefix.Length..].Trim()
            : First(request.Headers[ApiKeyHeader]).Trim();
    }

    /// <summary>The lowercase hexadecimal SHA-256 of a well-formed key, null otherwise.</summary>
    public static string? HashOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != Prefix.Length + SecretLength
            || !key.StartsWith(Prefix, StringComparison.Ordinal)
            || key.AsSpan(Prefix.Length).ContainsAnyExcept(LowerHex))
        {
            return null;
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(key)));
    }

    // The first line of a header, as Go's Header.Get reads it.
    private static string First(StringValues values) =>
        values.Count > 0 ? values[0] ?? string.Empty : string.Empty;
}
