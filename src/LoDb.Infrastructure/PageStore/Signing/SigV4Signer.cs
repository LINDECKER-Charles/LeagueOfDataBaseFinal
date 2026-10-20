using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LoDb.Infrastructure.PageStore.Signing;

/// <summary>
/// Signs an S3 request with AWS Signature Version 4, in the <c>Authorization</c> header: the
/// minimal subset an object PUT or HEAD needs (no query, no chunked payload).
/// </summary>
/// <remarks>
/// Hand-written instead of taking an S3 SDK: two verbs do not justify the dependency, and an
/// SDK's own defaults (checksum headers, chunked uploads) are what S3-compatible stores such
/// as Scaleway diverge on. The payload is always hashed, never sent as
/// <c>UNSIGNED-PAYLOAD</c>, so the store checks the body it receives.
/// </remarks>
internal sealed class SigV4Signer(SigV4Credentials credentials, string region)
{
    public const string ContentHashHeader = "x-amz-content-sha256";
    public const string DateHeader = "x-amz-date";

    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string Service = "s3";
    private const string Terminator = "aws4_request";
    private const string KeyPrefix = "AWS4";
    private const string AuthorizationHeader = "Authorization";
    private const string StampFormat = "yyyyMMdd'T'HHmmss'Z'";
    private const string DayFormat = "yyyyMMdd";

    /// <summary>The hash of an empty payload, the one a HEAD signs.</summary>
    public static readonly string EmptyPayloadHash = Hash([]);

    /// <summary>The lower-case hexadecimal SHA-256 of a payload.</summary>
    public static string Hash(ReadOnlySpan<byte> payload) =>
        Convert.ToHexStringLower(SHA256.HashData(payload));

    /// <summary>
    /// Stamps <paramref name="request"/> at <paramref name="signedAt"/> and signs it; a
    /// previous signature, if any, is replaced.
    /// </summary>
    public void Sign(HttpRequestMessage request, string payloadHash, DateTimeOffset signedAt)
    {
        var instant = signedAt.UtcDateTime;
        var stamp = instant.ToString(StampFormat, CultureInfo.InvariantCulture);
        var scope = string.Join(
            '/',
            instant.ToString(DayFormat, CultureInfo.InvariantCulture),
            region,
            Service,
            Terminator);
        Replace(request, AuthorizationHeader, value: null);
        Replace(request, DateHeader, stamp);
        Replace(request, ContentHashHeader, payloadHash);

        var canonical = SigV4CanonicalRequest.Of(request, payloadHash);
        var stringToSign = string.Join(
            '\n',
            Algorithm,
            stamp,
            scope,
            Hash(Encoding.UTF8.GetBytes(canonical.Text)));
        var signature = Convert.ToHexStringLower(
            Mac(SigningKey(instant), stringToSign));
        Replace(
            request,
            AuthorizationHeader,
            $"{Algorithm} Credential={credentials.AccessKey}/{scope}, "
                + $"SignedHeaders={canonical.SignedHeaders}, Signature={signature}");
    }

    private byte[] SigningKey(DateTime instant)
    {
        var day = Mac(
            Encoding.UTF8.GetBytes(KeyPrefix + credentials.SecretKey),
            instant.ToString(DayFormat, CultureInfo.InvariantCulture));
        return Mac(Mac(Mac(day, region), Service), Terminator);
    }

    private static byte[] Mac(byte[] key, string data) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static void Replace(HttpRequestMessage request, string name, string? value)
    {
        request.Headers.Remove(name);
        if (value is not null)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }
    }
}
