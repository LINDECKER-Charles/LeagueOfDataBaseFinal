using LoDb.Api.Modules.Admin.Prerender.Options;
using LoDb.Api.Modules.Admin.Prerender.Pace.Decision;
using LoDb.Api.Modules.Admin.Prerender.Pace.Telemetry;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Prerender.Pace;

/// <summary>
/// The pace of the pass on this instance: the renders a minute <c>RenderPacer</c> lets it
/// start, set by the regulator's decisions while the engine drains a pass here, the fixed pace
/// otherwise.
/// </summary>
/// <remarks>
/// <para>
/// Driven only from <c>PrerenderEngine.RunAsync</c>, under the pass's lock: the command line
/// drains a unit at the fixed pace, as does an instance whose background tasks are off. Held in
/// memory, as the engine's pulse: each drain starts again at the floor.
/// </para>
/// <para>
/// Fail-safe: an allowance no decision renewed for <see cref="PaceTiming.Stale"/> (the sampler
/// stopped) falls back to the floor, never to the faster fixed pace.
/// </para>
/// </remarks>
internal sealed class PaceRegulator
{
    private readonly Lock _gate = new();
    private readonly IOptions<PrerenderOptions> _options;
    private readonly TimeProvider _clock;
    private readonly PaceChanges _changes;
    private readonly ILogger<PaceRegulator> _logger;
    private PaceMemory? _memory;
    private long _passId;
    private long? _fixedPassId;
    private long _drive;
    private DateTimeOffset _decidedAt;
    private double? _hostBusy;

    public PaceRegulator(
        IOptions<PrerenderOptions> options,
        TimeProvider clock,
        PaceMetrics metrics,
        ILogger<PaceRegulator> logger)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        (_options, _clock, _logger) = (options, clock, logger);
        _changes = new PaceChanges(metrics, logger);
        metrics.Follow(Snapshot);
    }

    /// <summary>Whether the engine drains a pass here now.</summary>
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
            _memory = options.Pace.IsAdaptive
                ? PaceMemory.Start(now, options.Pace.MinRendersPerMinute)
                : PaceMemory.Fixed(now, options.RendersPerMinute);
            (_passId, _decidedAt, _hostBusy) = (passId, now, null);
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

    /// <summary>The engine stopped draining: the fixed pace applies again.</summary>
    public void End()
    {
        lock (_gate)
        {
            _memory = null;
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

    /// <summary>Applies one evaluation of the signals, while driven in adaptive mode.</summary>
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
            after = PaceDecision.Next(memory, evaluation, _options.Value.Pace);
            (_memory, _decidedAt, _hostBusy) = (after, evaluation.At, evaluation.Board.HostBusy);
        }

        _changes.Note(before, after, evaluation.Board.Worst);
    }

    /// <summary>The regulator now; null when no pass drains here.</summary>
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
                State = memory.State,
                RendersPerMinute = AllowanceOf(memory, _clock.GetUtcNow()),
                Reason = memory.Reason,
                HostBusy = _hostBusy,
                HeldUntil = memory.HeldUntil,
                Since = memory.Since,
            };
        }
    }

    private int AllowanceOf(PaceMemory? memory, DateTimeOffset now)
    {
        var options = _options.Value;
        if (memory is null || memory.State == PaceState.Fixed)
        {
            return options.RendersPerMinute;
        }

        return now - _decidedAt > PaceTiming.Stale
            ? options.Pace.MinRendersPerMinute
            : memory.Allowance;
    }
}
