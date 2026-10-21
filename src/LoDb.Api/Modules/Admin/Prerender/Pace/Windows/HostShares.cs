namespace LoDb.Api.Modules.Admin.Prerender.Pace.Windows;

/// <summary>
/// The host's CPU shares (0..1), each null while it cannot be told: busy and stolen over the
/// last minute, used (busy without the steal) over the last 30 minutes.
/// </summary>
internal sealed record HostShares(double? Busy, double? Steal, double? Used)
{
    public static HostShares Unknown { get; } = new(null, null, null);
}
