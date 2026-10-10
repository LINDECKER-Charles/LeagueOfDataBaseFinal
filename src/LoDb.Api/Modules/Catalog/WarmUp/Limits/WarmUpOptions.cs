namespace LoDb.Api.Modules.Catalog.WarmUp.Limits;

/// <summary>
/// Bounds of the patch-switch loader (<c>LoDb:Catalog:WarmUp</c>), checked when the host
/// starts.
/// </summary>
/// <remarks>
/// A stream holds a connection and may start ingestions for minutes: the bounds keep a few
/// clients from holding them all. A refused or expired stream ends with a failed frame, and
/// the client visits the page anyway.
/// </remarks>
internal sealed class WarmUpOptions
{
    public const string SectionName = "LoDb:Catalog:WarmUp";

    private const int DefaultMaxStreams = 8;
    private const int DefaultMaxStreamsPerClient = 2;
    private const int DefaultStreamLifetimeSeconds = 120;
    private const int DefaultPerClientPerHour = 20;

    /// <summary>Streams open at once on the instance.</summary>
    public int MaxStreams { get; set; } = DefaultMaxStreams;

    /// <summary>Streams open at once for one address, or one IPv6 /64.</summary>
    public int MaxStreamsPerClient { get; set; } = DefaultMaxStreamsPerClient;

    /// <summary>How long a stream may stay open before it ends, failed.</summary>
    public TimeSpan StreamLifetime { get; set; } =
        TimeSpan.FromSeconds(DefaultStreamLifetimeSeconds);

    /// <summary>
    /// Warm-ups one address, or one IPv6 /64, may start per sliding hour; beyond, 429.
    /// </summary>
    public int PerClientPerHour { get; set; } = DefaultPerClientPerHour;
}
