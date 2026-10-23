using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Manual;
using LoDb.Api.Modules.Admin.Prerender.Pace.Resources;

namespace LoDb.Api.Modules.Admin.Prerender.Http.Speed;

/// <summary>The speed report's parts, from the setting, the options and the readings.</summary>
internal static class SpeedViews
{
    private const int Decimals = 3;
    private const int SecondsPerMinute = 60;

    public static PrerenderSpeedSettingView SettingOf(PaceSetting setting, PaceOptions pace)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(pace);
        return new PrerenderSpeedSettingView
        {
            Mode = PaceSettingText.Of(setting.Mode),
            RendersPerMinute = Math.Min(setting.RendersPerMinute, pace.ManualMaxRendersPerMinute),
            UpdatedAt = setting.UpdatedAt,
        };
    }

    public static PrerenderSpeedLimits LimitsOf(PrerenderOptions options, ResourceReadings? read)
    {
        ArgumentNullException.ThrowIfNull(options);
        var pace = options.Pace;
        var measured = read?.RendererCpuSecondsPerPage;
        var cost = measured ?? PageCost.DefaultRendererCpuSecondsPerPage;
        return new PrerenderSpeedLimits
        {
            MinRendersPerMinute = pace.MinRendersPerMinute,
            MaxRendersPerMinute = pace.MaxRendersPerMinute,
            ManualMaxRendersPerMinute = pace.ManualMaxRendersPerMinute,
            ManualWarnRendersPerMinute = pace.ManualWarnRendersPerMinute,
            Renderers = options.Renderers,
            RendererCpus = Round(pace.RendererCpus),
            BrakeHostBusy = Round(ManualDecision.BrakeHostBusy),
            BrakeMinutes = (int)(ManualDecision.UnsafeDecisionsToBrake
                * PaceTiming.SamplePeriod.TotalSeconds
                * PaceTiming.SamplesPerDecision / SecondsPerMinute),
            HoldMinutes = (int)PaceTiming.Hold.TotalMinutes,
            RendererCpuSecondsPerPage = Round(cost),
            IsCostMeasured = measured is not null,
            StackCpuSecondsPerPage = Round(PageCost.StackCpuSecondsPerPage),
        };
    }

    public static PrerenderResourcesView? ResourcesOf(ResourceReadings? read) => read is null
        ? null
        : new PrerenderResourcesView
        {
            At = read.At,
            HostCpus = read.HostCpus,
            HostBusy = Round(read.HostBusy),
            HostSteal = Round(read.HostSteal),
            Renderers = read.Renderers,
            RenderersCores = Round(read.RenderersCores),
            RenderersPerMinute = Round(read.RenderersPerMinute),
            VisitorsCores = Round(read.VisitorsCores),
            VisitorsRefused = read.VisitorsRefused,
            OlderRefusedPerMinute = Round(read.OlderRefusedPerMinute),
            ApiCores = Round(read.ApiCores),
        };

    private static decimal Round(double value) => Math.Round((decimal)value, Decimals);

    private static decimal? Round(double? value) => value is { } read ? Round(read) : null;
}
