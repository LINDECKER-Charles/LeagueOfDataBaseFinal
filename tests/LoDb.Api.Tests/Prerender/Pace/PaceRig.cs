using System.Diagnostics.Metrics;
using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
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
        Regulator = new PaceRegulator(
            Microsoft.Extensions.Options.Options.Create(Options),
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
