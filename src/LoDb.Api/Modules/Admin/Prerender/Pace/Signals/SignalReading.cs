using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

/// <summary>One signal's level, the reason it stands for, and the value it read.</summary>
internal sealed record SignalReading(SignalLevel Level, PaceReason Reason, double? Value)
{
    /// <summary>Nothing to say: the pace may climb.</summary>
    public static SignalReading Green { get; } = new(SignalLevel.Green, PaceReason.None, null);

    /// <summary>A source that cannot be read slows the pass, never lets it run faster.</summary>
    public static SignalReading Missing { get; } =
        new(SignalLevel.Amber, PaceReason.SignalMissing, null);
}
