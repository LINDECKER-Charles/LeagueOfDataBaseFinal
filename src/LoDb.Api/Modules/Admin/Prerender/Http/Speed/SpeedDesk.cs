using LoDb.Api.Modules.Accounts.Http;
using LoDb.Api.Modules.Admin.Prerender.Http.Monitor;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Manual;
using LoDb.Api.Modules.Admin.Prerender.Pace.Resources;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Speed;

/// <summary>
/// The admin's speed setting of the pre-rendering: its report (the stored setting, what the
/// environment allows, the pace in force, the host's resources) and its change, applied at once
/// on this instance and within 30 s on the one draining the pass.
/// </summary>
internal sealed class SpeedDesk(
    PaceSettingStore store,
    PaceSettings settings,
    ResourceBoard resources,
    PulseReader pulse,
    PrerenderActivation activation,
    IOptions<PrerenderOptions> options,
    TimeProvider clock)
{
    /// <summary>409: the environment's kill switch holds the fixed pace.</summary>
    public const string Fixed = "prerender-pace-fixed";

    public async Task<PrerenderSpeedReport> ReportAsync(CancellationToken cancellationToken)
    {
        var value = options.Value;
        var setting = await StoredAsync(cancellationToken);
        var read = resources.Readings;
        return new PrerenderSpeedReport
        {
            GeneratedAt = clock.GetUtcNow(),
            IsConfigured = activation.IsActive,
            IsFixed = !value.Pace.IsAdaptive,
            Setting = SpeedViews.SettingOf(setting, value.Pace),
            Limits = SpeedViews.LimitsOf(value, read),
            Pace = pulse.Pace(),
            Resources = SpeedViews.ResourcesOf(read),
        };
    }

    /// <summary>
    /// Stores the pace <paramref name="request"/> asks for; refused under the kill switch and
    /// for a target out of the environment's bounds.
    /// </summary>
    public async Task<AccountProblem?> SetAsync(
        PrerenderSpeedRequest request,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Pace.IsAdaptive)
        {
            return PrerenderProblems.Conflict(Fixed);
        }

        var current = await StoredAsync(cancellationToken);
        var max = options.Value.Pace.ManualMaxRendersPerMinute;
        var (setting, problem) = SpeedRequestReader.Read(request, current, max);
        if (setting is null)
        {
            return problem;
        }

        await store.SaveAsync(setting, cancellationToken);
        settings.Apply(setting with { UpdatedAt = clock.GetUtcNow() });
        return null;
    }

    private async Task<PaceSetting> StoredAsync(CancellationToken cancellationToken) =>
        await store.ReadAsync(cancellationToken) ?? PaceSetting.DefaultOf(options.Value.Pace);
}
