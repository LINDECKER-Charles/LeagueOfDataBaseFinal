using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Manual;
using LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Pace;

/// <summary>
/// The pace of the pre-rendering on this instance: the renders a minute <c>RenderPacer</c> lets
/// the pass and the automatic queue start together, set by the regulator's decisions while the
/// engine drains a pass or the queue drains pages here (adaptive), or by the operator's target
/// (manual, <see cref="PaceSettings"/>), the fixed pace otherwise.
/// </summary>
/// <remarks>
/// <para>
/// Driven from <c>PrerenderEngine.RunAsync</c>, under the pass's lock, and from the drain of
/// each automatic queue (the refused pages, the closed states), under its own
/// (<see cref="BeginQueue"/>): the command line drains a unit at the fixed pace, as does an
/// instance whose background tasks are off. Held in memory, as the engine's pulse: each pass
/// starts again at the floor, or at the target; a queue joins a pace already driven as it is,
/// and the pace stops once neither the pass nor any queue drives it.
/// </para>
/// <para>
/// A new target applies at once; a change of mode at the next decision, within 30 s.
/// Fail-safe: an allowance no decision renewed for <see cref="PaceTiming.Stale"/> (the sampler
/// stopped, and with it the manual mode's brake) falls back to the floor, never to the faster
/// fixed pace nor to the target.
/// </para>
/// </remarks>
internal sealed class PaceRegulator
{
    // The pass id of a pace the queue drives alone.
    private const long NoPass = 0;

    private readonly Lock _gate = new();
    private readonly IOptions<PrerenderOptions> _options;
    private readonly PaceSettings _settings;
    private readonly TimeProvider _clock;
    private readonly PaceChanges _changes;
    private readonly ILogger<PaceRegulator> _logger;
    private PaceMemory? _memory;
    private long _passId;
    private bool _isPassDriving;
    private readonly HashSet<string> _queuesDriving = new(StringComparer.Ordinal);
    private long? _fixedPassId;
    private long _drive;
    private DateTimeOffset _decidedAt;
    private double? _hostBusy;

    public PaceRegulator(
        IOptions<PrerenderOptions> options,
        PaceSettings settings,
        TimeProvider clock,
        PaceMetrics metrics,
        ILogger<PaceRegulator> logger)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        (_options, _settings, _clock, _logger) = (options, settings, clock, logger);
        _changes = new PaceChanges(metrics, logger);
        metrics.Follow(Snapshot);
    }

    /// <summary>Whether the engine drains a pass, or the queue its pages, here now.</summary>
    public bool IsDriven
    {
        get
        {
            lock (_gate)
            {
                return _memory is not null;
            }
        }
    }

    /// <summary>Grows at each drain: the sampler forgets what it read of the one before.</summary>
    public long Drive => Interlocked.Read(ref _drive);

    /// <summary>The engine starts draining <paramref name="passId"/>: at the floor.</summary>
    public void Begin(long passId)
    {
        var options = _options.Value;
        var now = _clock.GetUtcNow();
        bool isNewFixedPass;
        lock (_gate)
        {
            _memory = FirstMemory(now);
            (_passId, _decidedAt, _hostBusy) = (passId, now, null);
            _isPassDriving = true;
            isNewFixedPass = !options.Pace.IsAdaptive && _fixedPassId != passId;
            _fixedPassId = options.Pace.IsAdaptive ? null : passId;
            Interlocked.Increment(ref _drive);
        }

        // Once a pass: a pass whose units all wait is drained again every half minute.
        if (isNewFixedPass)
        {
            PaceLog.Fixed(_logger, options.RendersPerMinute);
        }
    }

    /// <summary>
    /// The engine stopped draining: the fixed pace applies again, unless the queue drives.
    /// </summary>
    public void End()
    {
        lock (_gate)
        {
            (_isPassDriving, _passId) = (false, NoPass);
            _memory = _queuesDriving.Count > 0 ? _memory : null;
        }
    }

    /// <summary>
    /// The queue named <paramref name="queue"/> drains pages: from the floor, or the target,
    /// unless a pass or another queue already drives the pace, which it then shares as it is.
    /// Called again, it changes nothing.
    /// </summary>
    public void BeginQueue(string queue)
    {
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            _queuesDriving.Add(queue);
            if (_memory is not null)
            {
                return;
            }

            _memory = FirstMemory(now);
            (_passId, _decidedAt, _hostBusy) = (NoPass, now, null);
            Interlocked.Increment(ref _drive);
        }
    }

    /// <summary>
    /// The queue named <paramref name="queue"/> stopped draining: the fixed pace applies again,
    /// unless a pass or another queue drives.
    /// </summary>
    public void EndQueue(string queue)
    {
        lock (_gate)
        {
            _queuesDriving.Remove(queue);
            _memory = _isPassDriving || _queuesDriving.Count > 0 ? _memory : null;
        }
    }

    /// <summary>Renders a minute the pass may start now: 0 while held.</summary>
    public int Allowance()
    {
        lock (_gate)
        {
            return AllowanceOf(_memory, _clock.GetUtcNow());
        }
    }

    /// <summary>
    /// Applies one evaluation of the signals while driven, adaptive or manual: the fixed mode
    /// reads none.
    /// </summary>
    public void Decide(PaceEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        PaceMemory before;
        PaceMemory after;
        lock (_gate)
        {
            if (_memory is not { State: not PaceState.Fixed } memory)
            {
                return;
            }

            before = memory;
            after = Next(memory, evaluation);
            (_memory, _decidedAt, _hostBusy) = (after, evaluation.At, evaluation.Board.HostBusy);
        }

        _changes.Note(before, after, after.IsManual
            ? ManualDecision.ReadingOf(after, evaluation.Board)
            : evaluation.Board.Worst);
    }

    /// <summary>The regulator now; null when neither a pass nor the queue drains here.</summary>
    public PaceSnapshot? Snapshot()
    {
        lock (_gate)
        {
            if (_memory is not { } memory)
            {
                return null;
            }

            return new PaceSnapshot
            {
                PassId = _passId,
                Mode = _options.Value.Pace.Mode,
                IsManual = memory.IsManual,
                State = memory.State,
                RendersPerMinute = AllowanceOf(memory, _clock.GetUtcNow()),
                Reason = memory.Reason,
                HostBusy = _hostBusy,
                HeldUntil = memory.HeldUntil,
                Since = memory.Since,
            };
        }
    }

    private PaceMemory FirstMemory(DateTimeOffset now)
    {
        var options = _options.Value;
        return !options.Pace.IsAdaptive ? PaceMemory.Fixed(now, options.RendersPerMinute)
            : _settings.IsManual ? PaceMemory.Manual(now, _settings.ManualTarget)
            : PaceMemory.Start(now, options.Pace.MinRendersPerMinute);
    }

    // Back from manual to adaptive, the regulator climbs from the floor again; a manual
    // memory held by the brake keeps its hold, the adaptive rule then releasing it.
    private PaceMemory Next(PaceMemory memory, PaceEvaluation evaluation)
    {
        var pace = _options.Value.Pace;
        if (_settings.IsManual)
        {
            return ManualDecision.Next(memory, evaluation, _settings.ManualTarget);
        }

        return memory.State == PaceState.Manual
            ? PaceMemory.Start(evaluation.At, pace.MinRendersPerMinute)
            : PaceDecision.Next(memory with { IsManual = false }, evaluation, pace);
    }

    private int AllowanceOf(PaceMemory? memory, DateTimeOffset now)
    {
        var options = _options.Value;
        if (memory is null || memory.State == PaceState.Fixed)
        {
            return options.RendersPerMinute;
        }

        if (now - _decidedAt > PaceTiming.Stale)
        {
            return options.Pace.MinRendersPerMinute;
        }

        return memory.State switch
        {
            PaceState.Held => 0,
            PaceState.Manual => _settings.IsManual
                ? _settings.ManualTarget
                : options.Pace.MinRendersPerMinute,
            _ => memory.Allowance,
        };
    }
}
