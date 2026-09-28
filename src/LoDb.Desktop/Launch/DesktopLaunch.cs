using LoDb.Desktop.Hosting;

namespace LoDb.Desktop.Launch;

/// <summary>A command line, read: what to run and with which settings.</summary>
internal sealed record DesktopLaunch
{
    public required LaunchMode Mode { get; init; }

    public required DesktopOptions Options { get; init; }

    /// <summary>
    /// <c>--probe-api</c>: the smoke check also calls the remote API through the proxy. Off
    /// by default, so that the check of a build needs no network.
    /// </summary>
    public bool ShouldProbeApi { get; init; }
}
