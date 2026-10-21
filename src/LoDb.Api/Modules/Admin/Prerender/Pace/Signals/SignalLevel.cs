namespace LoDb.Api.Modules.Admin.Prerender.Pace.Signals;

/// <summary>What a signal tells the regulator, from the mildest to the gravest.</summary>
internal enum SignalLevel
{
    /// <summary>The pace may climb.</summary>
    Green,

    /// <summary>The pace stays: the band between climbing and backing off.</summary>
    Hold,

    /// <summary>The pace halves.</summary>
    Amber,

    /// <summary>No new render starts for a while.</summary>
    Red,
}
