using System.Globalization;
using System.Threading.RateLimiting;

namespace LoDb.Api.Modules.PublicApi.Limits;

/// <summary>
/// The answer of a key's token bucket, and the <c>X-RateLimit-*</c> headers it gives every
/// response once the key is known.
/// </summary>
/// <param name="Allowed">Whether a token was taken.</param>
/// <param name="Limit">Capacity of the bucket.</param>
/// <param name="Remaining">Whole tokens left.</param>
/// <param name="Reset">
/// Unix time when the bucket is full again (allowed) or holds a token (refused).
/// </param>
internal sealed record RateLimitVerdict(bool Allowed, int Limit, int Remaining, long Reset)
{
    public const string LimitHeader = "X-RateLimit-Limit";
    public const string RemainingHeader = "X-RateLimit-Remaining";
    public const string ResetHeader = "X-RateLimit-Reset";

    /// <summary>Where a lease of a bucket carries its verdict.</summary>
    public static MetadataName<RateLimitVerdict> MetadataName { get; } =
        new("lodb.publicapi.verdict");

    /// <summary>A key allowed no request at all: go-api refuses it without a bucket.</summary>
    public static RateLimitVerdict Closed(DateTimeOffset now) =>
        new(Allowed: false, Limit: 0, Remaining: 0, Reset: now.ToUnixTimeSeconds());

    public void WriteHeaders(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        headers[LimitHeader] = Limit.ToString(CultureInfo.InvariantCulture);
        headers[RemainingHeader] = Remaining.ToString(CultureInfo.InvariantCulture);
        headers[ResetHeader] = Reset.ToString(CultureInfo.InvariantCulture);
    }
}
