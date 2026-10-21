using System.Diagnostics.Metrics;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Manual;
using LoDb.Api.Modules.Admin.Prerender.Pace.Resources;
using LoDb.Api.Modules.Admin.Prerender.Pace.Signals;
using LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.Prerender.Pace;

/// <summary>
/// A regulator as production sets it (floor 30, ceiling 240, step 30, fixed pace 120) on a
/// clock the test moves, its logs and its meter in memory.
/// </summary>
internal sealed class PaceRig : IDisposable
{
    public const int Floor = 30;
    public const int Ceiling = 240;
    public const int FixedPace = 120;

    private ResourceBoard? _resources;

    private readonly ServiceProvider _meters = new ServiceCollection().AddMetrics()
        .BuildServiceProvider();

    public PaceRig(PaceMode mode = PaceMode.Adaptive)
    {
        Options = new PrerenderOptions
        {
            PublicHost = "league-of-data-base.com",
            SsrBaseUrl = "http://lodb-web-ssr-prerender:4000",
            RendersPerMinute = FixedPace,
            Pace = new PaceOptions
            {
                Mode = mode,
                VisitorsLoadUrl = "http://lodb-web-ssr:9464/load",
                PassLoadUrl = "http://lodb-web-ssr-prerender:9464/load",
            },
        };
        var options = Microsoft.Extensions.Options.Options.Create(Options);
        Settings = new PaceSettings(options, new FakeLogger<PaceSettings>(Logs));
        Regulator = new PaceRegulator(
            options,
            Settings,
            Clock,
            new PaceMetrics(Meters),
            new FakeLogger<PaceRegulator>(Logs));
    }

    public PrerenderOptions Options { get; }

    public FakeTimeProvider Clock { get; } =
        new(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));

    public FakeLogCollector Logs { get; } = new();

    public IMeterFactory Meters => _meters.GetRequiredService<IMeterFactory>();

    public PaceRegulator Regulator { get; }

    /// <summary>The resources the sampler notes.</summary>
    public ResourceBoard Resources => _resources ??= new ResourceBoard(Clock);

    /// <summary>
    /// The operator's setting the regulator reads: adaptive until a test sets it.
    /// </summary>
    public PaceSettings Settings { get; }

    /// <summary>Sets the manual mode at <paramref name="target"/> renders a minute.</summary>
    public void Manual(int target) => Settings.Apply(
        new PaceSetting(PaceChoice.Manual, target) { UpdatedAt = Clock.GetUtcNow() });

    /// <summary>Moves the clock on, then decides on the manual brake's two readings.</summary>
    public void DecideManual(int seconds, double? hostBusy, long? visitorsRefused)
    {
        Clock.Advance(TimeSpan.FromSeconds(seconds));
        var board = new PaceBoard(SignalReading.Green, hostBusy, visitorsRefused);
        Regulator.Decide(new PaceEvaluation(board, Clock.GetUtcNow(), Ceiling));
    }

    /// <summary>The event names logged so far, in order.</summary>
    public IReadOnlyList<string> EventNames =>
        [.. Logs.GetSnapshot().Select(static entry => entry.Id.Name ?? string.Empty)];

    /// <summary>Moves the clock <paramref name="seconds"/> on, then decides on one signal.
    /// </summary>
    public void DecideAfter(int seconds, SignalLevel level, int? achieved = null)
    {
        Clock.Advance(TimeSpan.FromSeconds(seconds));
        Regulator.Decide(Evaluation(level, Clock.GetUtcNow(), achieved ?? Ceiling));
    }

    /// <summary>One evaluation, its worst signal the host's CPU at <paramref name="level"/>.
    /// </summary>
    public static PaceEvaluation Evaluation(SignalLevel level, DateTimeOffset at, int achieved)
    {
        var reason = level == SignalLevel.Green ? PaceReason.None : PaceReason.HostCpu;
        var board = new PaceBoard(new SignalReading(level, reason, 0.42), 0.42);
        return new PaceEvaluation(board, at, achieved);
    }

    public void Dispose() => _meters.Dispose();
}
