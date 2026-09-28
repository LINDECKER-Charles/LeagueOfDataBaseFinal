using System.Globalization;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Catalog.Http;

/// <summary>
/// How long a catalog answer may be reused, from how final its content is.
/// </summary>
/// <remarks>
/// Every catalog URL names its version, so an answer about a version older than the latest
/// one never changes: shared caches keep it a day. The latest version, one newer than it
/// (ingested on demand before its promotion) and an answer given before any promotion stay
/// short, served stale while they revalidate. An answer still showing placeholders asks for
/// one retry through <c>Retry-After</c> and is kept only until then.
/// </remarks>
internal sealed record CacheHeaders
{
    /// <summary>Delay before the one retry a client makes on placeholders or a 503.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public static CacheHeaders Frozen { get; } = new()
    {
        CacheControl = "public, max-age=86400",
    };

    public static CacheHeaders Current { get; } = new()
    {
        CacheControl = "public, max-age=60, stale-while-revalidate=600",
    };

    public static CacheHeaders Pending { get; } = new()
    {
        CacheControl = "public, max-age=5",
        RetryAfter = RetryDelay,
    };

    public required string CacheControl { get; init; }

    public TimeSpan? RetryAfter { get; init; }

    /// <summary>The caching of a complete answer about <paramref name="version"/>.</summary>
    public static CacheHeaders Of(PatchVersion version, PatchVersion? latest) =>
        latest is not null && version < latest ? Frozen : Current;

    /// <summary>A delay as the whole seconds <c>Retry-After</c> takes.</summary>
    public static string Seconds(TimeSpan delay) =>
        ((long)Math.Ceiling(delay.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
}
