namespace LoDb.Api.Modules.Admin.Prerender.Pace.Decision;

/// <summary>
/// The regulator's clock (docs/produit/pre-rendu-catalogue-cdn.md, lot 2): it samples every
/// 10 s, decides every 30 s, climbs at most a step a minute after two green decisions, and
/// backs off at once.
/// </summary>
internal static class PaceTiming
{
    /// <summary>The host and the API's cgroup are sampled this often.</summary>
    public static readonly TimeSpan SamplePeriod = TimeSpan.FromSeconds(10);

    /// <summary>Samples between two decisions: one every 30 s.</summary>
    public const int SamplesPerDecision = 3;

    /// <summary>The window of the short signals: the host's busy share, the pass's median.
    /// </summary>
    public static readonly TimeSpan ShortWindow = TimeSpan.FromMinutes(1);

    /// <summary>The window of the host's sustained share and of the pass's baseline.</summary>
    public static readonly TimeSpan LongWindow = TimeSpan.FromMinutes(30);

    /// <summary>Samples in <see cref="ShortWindow"/>.</summary>
    public const int ShortWindowSamples = 6;

    /// <summary>Samples in <see cref="LongWindow"/>.</summary>
    public const int LongWindowSamples = 180;

    /// <summary>No increase this long after a drain starts: the signals need a minute.</summary>
    public static readonly TimeSpan WarmUp = TimeSpan.FromSeconds(60);

    /// <summary>The least time between two increases.</summary>
    public static readonly TimeSpan IncreaseSpacing = TimeSpan.FromSeconds(60);

    /// <summary>No increase this long after a decrease.</summary>
    public static readonly TimeSpan DecreaseCooldown = TimeSpan.FromMinutes(3);

    /// <summary>A red signal holds new renders this long, from its last red decision.</summary>
    public static readonly TimeSpan Hold = TimeSpan.FromMinutes(5);

    /// <summary>An allowance not renewed for this long falls back to the floor.</summary>
    public static readonly TimeSpan Stale = TimeSpan.FromSeconds(90);

    /// <summary>A page held back looks again whether its pass still runs this often.</summary>
    public static readonly TimeSpan HeldRecheck = TimeSpan.FromSeconds(10);

    /// <summary>Green decisions in a row before an increase.</summary>
    public const int GreenStreakToIncrease = 2;

    /// <summary>
    /// The share of its allowance the pass must have started over the last minute for an
    /// increase: a pass that waits on a catalog does not inflate a pace it does not use.
    /// </summary>
    public const double AchievedShareToIncrease = 0.8;
}
