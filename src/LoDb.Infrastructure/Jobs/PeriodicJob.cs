using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoDb.Infrastructure.Jobs;

/// <summary>
/// Base of the periodic background jobs (ADR 0003): a <see cref="PeriodicTimer"/>, an
/// advisory lock, a shared schedule, metrics and one summary line per run.
/// </summary>
/// <remarks>
/// <para>
/// A concrete job lives under <c>LoDb.Api/Workers/&lt;Zone&gt;/</c>, where the convention
/// registers it, and only gives a name, a period and the work of one run.
/// </para>
/// <para>
/// A tick runs the job only if this instance takes the lock and the job did not start, on
/// any instance, less than a period ago (minus a tolerance of a tenth of the period, one
/// minute at most, so that a timer firing early still runs). The job therefore runs once
/// per period whatever the number of instances, restarts included, and runs never overlap.
/// A failure is logged and measured, never thrown: the next period tries again.
/// </para>
/// </remarks>
public abstract partial class PeriodicJob(PeriodicJobServices services) : BackgroundService
{
    private const int MaximumNameLength = 64;
    private static readonly TimeSpan MaximumTolerance = TimeSpan.FromMinutes(1);

    private readonly ILogger _logger = services.LoggerFactory.CreateLogger<PeriodicJob>();

    /// <summary>
    /// Stable name, 64 characters at most: lock key, <c>periodic_job</c> row, metric tag.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>Time between two runs; the first tick is at start.</summary>
    public abstract TimeSpan Period { get; }

    /// <summary>
    /// Runs one tick: takes the lock, checks the schedule, then runs the job if it is due.
    /// </summary>
    public async Task<JobRunOutcome> RunIfDueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var held = await services.Lock.TryAcquireAsync(
                "job:" + Name,
                cancellationToken);
            if (held is null)
            {
                return JobRunOutcome.Locked;
            }

            var due = await services.Schedule.TryStartAsync(
                Name,
                services.TimeProvider.GetUtcNow(),
                Period - Tolerance(Period),
                cancellationToken);
            return due ? await RunAsync(cancellationToken) : JobRunOutcome.NotDue;
        }
        catch (Exception exception) when (!IsStopping(exception, cancellationToken))
        {
            services.Metrics.RecordFailure(Name, duration: null);
            LogScheduleFailed(_logger, Name, exception);
            return JobRunOutcome.Failed;
        }
    }

    /// <summary>The work of one run.</summary>
    /// <returns>What the run processed, for the summary line.</returns>
    protected abstract Task<JobRunSummary> RunOnceAsync(CancellationToken cancellationToken);

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaximumNameLength
            || Period <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"Job {GetType().Name} needs a name of 1 to {MaximumNameLength} characters"
                + " and a positive period.");
        }

        using var timer = new PeriodicTimer(Period, services.TimeProvider);
        do
        {
            await RunIfDueAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private static TimeSpan Tolerance(TimeSpan period) =>
        period / 10 < MaximumTolerance ? period / 10 : MaximumTolerance;

    private static bool IsStopping(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private async Task<JobRunOutcome> RunAsync(CancellationToken cancellationToken)
    {
        var time = services.TimeProvider;
        var started = time.GetTimestamp();
        try
        {
            var summary = await RunOnceAsync(cancellationToken);
            var finishedAt = time.GetUtcNow();
            await services.Schedule.RecordSuccessAsync(Name, finishedAt, cancellationToken);
            var duration = time.GetElapsedTime(started);
            services.Metrics.RecordSuccess(Name, duration, finishedAt);
            LogSucceeded(
                _logger,
                Name,
                summary.Count,
                summary.Unit,
                (long)duration.TotalMilliseconds);
            return JobRunOutcome.Succeeded;
        }
        catch (Exception exception) when (!IsStopping(exception, cancellationToken))
        {
            var duration = time.GetElapsedTime(started);
            services.Metrics.RecordFailure(Name, duration);
            LogFailed(_logger, Name, (long)duration.TotalMilliseconds, exception);
            return JobRunOutcome.Failed;
        }
    }

    [LoggerMessage(
        EventName = "jobs.run.succeeded",
        Level = LogLevel.Information,
        Message = "Job {Job} processed {Count} {Unit} in {DurationMs} ms.")]
    private static partial void LogSucceeded(
        ILogger logger,
        string job,
        long count,
        string unit,
        long durationMs);

    [LoggerMessage(
        EventName = "jobs.run.failed",
        Level = LogLevel.Error,
        Message = "Job {Job} failed after {DurationMs} ms.")]
    private static partial void LogFailed(
        ILogger logger,
        string job,
        long durationMs,
        Exception exception);

    [LoggerMessage(
        EventName = "jobs.schedule.failed",
        Level = LogLevel.Warning,
        Message = "Job {Job} could not take its lock or read its schedule.")]
    private static partial void LogScheduleFailed(ILogger logger, string job, Exception exception);
}
