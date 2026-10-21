namespace LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

/// <summary>
/// Where a signal that grows with the load turns hold, amber and red; a band without a hold
/// zone holds at its amber bound, one without red stays amber.
/// </summary>
internal sealed record SignalBand(double HoldAt, double AmberAt, double? RedAt)
{
    public SignalLevel LevelOf(double value) =>
        value >= RedAt ? SignalLevel.Red
        : value >= AmberAt ? SignalLevel.Amber
        : value >= HoldAt ? SignalLevel.Hold
        : SignalLevel.Green;
}
