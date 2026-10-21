using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Pace.Manual;

/// <summary>
/// The operator's choice of pace as this instance knows it: applied at once by the admin's
/// request it answers, and read again from the database every 30 s by <c>PaceSampler</c>, so
/// that the instance draining the pass follows a choice another instance stored.
/// </summary>
/// <remarks>
/// A target over the environment's hard maximum (lowered since it was stored) reads as that
/// maximum: the environment always has the last word on how fast the pass may go.
/// </remarks>
internal sealed class PaceSettings(
    IOptions<PrerenderOptions> options,
    ILogger<PaceSettings> logger)
{
    private PaceSetting? _current;

    /// <summary>The setting in force; the default until one is read or set.</summary>
    public PaceSetting Current =>
        Volatile.Read(ref _current) ?? PaceSetting.DefaultOf(options.Value.Pace);

    public bool IsManual => Current.IsManual;

    /// <summary>The manual target in force, bounded by the hard maximum.</summary>
    public int ManualTarget =>
        Math.Min(Current.RendersPerMinute, options.Value.Pace.ManualMaxRendersPerMinute);

    /// <summary>Takes <paramref name="setting"/>, said once when it changes the pace.</summary>
    public void Apply(PaceSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        var before = Interlocked.Exchange(ref _current, setting);
        if (before is not null && before.Means(setting))
        {
            return;
        }

        if (before is null && setting.UpdatedAt is null)
        {
            return;
        }

        var mode = PaceSettingText.Of(setting.Mode);
        var target = Math.Min(
            setting.RendersPerMinute,
            options.Value.Pace.ManualMaxRendersPerMinute);
        PaceLog.SettingApplied(logger, mode, target);
    }
}
