using LoDb.Api.Modules.Admin.Scaleway.Views;
using Microsoft.Extensions.Options;

namespace LoDb.Api.Modules.Admin.Scaleway;

/// <summary>
/// The Scaleway bill of the admin panels, read on demand and kept an hour: Scaleway updates
/// its amounts a few times a day, and an admin page must not wait on it at each visit.
/// </summary>
/// <remarks>
/// The requests that overlap share one reading, which no request can cancel: one that ends
/// first leaves it to the others. The six months are asked at once, and a reading gives up
/// after <see cref="Deadline"/>, well before the panels stop waiting. A failed reading,
/// whatever its cause, keeps the last good bill, flagged stale, and the next attempt waits
/// five minutes. Nothing is stored: a restart reads again.
/// </remarks>
internal sealed class ScalewayBillReader(
    ScalewayBillingClient client,
    IOptions<ScalewayBillingOptions> options,
    TimeProvider clock,
    ILogger<ScalewayBillReader> logger)
{
    /// <summary>How long a good reading is shown before the next one.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    /// <summary>How long a failed reading holds back the next attempt.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long a whole reading may take, every month and page included: the admin client
    /// gives up after 30 s, and an answer must reach it before.
    /// </summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);

    private readonly Lock _sync = new();
    private ScalewayBillReport? _good;
    private DateTimeOffset? _failedAt;
    private Task? _reading;

    /// <summary>
    /// The bill: the kept one while it is fresh and of the current month, a new reading
    /// otherwise, unless the last one failed less than <see cref="RetryDelay"/> ago.
    /// </summary>
    public async Task<ScalewayBillReport> ReadAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return ScalewayBillReport.NotConfigured;
        }

        if (StartIfDue() is { } reading)
        {
            await reading.WaitAsync(cancellationToken);
        }

        return Current();
    }

    private Task? StartIfDue()
    {
        lock (_sync)
        {
            if (_reading is null && IsDue(clock.GetUtcNow()))
            {
                // On the pool, while the lock is held: the reading clears this field when it
                // ends, which it cannot do before the field is set.
                _reading = Task.Run(ReadAndKeepAsync, CancellationToken.None);
            }

            return _reading;
        }
    }

    private bool IsDue(DateTimeOffset now)
    {
        if (_failedAt is { } failedAt && now - failedAt < RetryDelay)
        {
            return false;
        }

        return _good is not { FetchedAt: { } fetchedAt }
            || now - fetchedAt >= Lifetime
            || !IsOfMonth(_good, now);
    }

    // A bill read before midnight UTC on the last day of a month names the month just ended
    // as the current one: the new month starts a new reading.
    private static bool IsOfMonth(ScalewayBillReport good, DateTimeOffset now) =>
        good.Backups?.Periods is [{ Period: var newest }, ..]
        && string.Equals(newest, BillingPeriods.EndingAt(now)[0], StringComparison.Ordinal);

    private ScalewayBillReport Current()
    {
        lock (_sync)
        {
            var isStale = _failedAt is not null;
            return _good is { } good
                ? good with { Stale = isStale }
                : new ScalewayBillReport { Configured = true, Stale = isStale };
        }
    }

    // Whatever goes wrong, an error status, an answer that is not the bill, the deadline, the
    // admin gets the last good bill rather than an error: hence every exception caught.
    private async Task ReadAndKeepAsync()
    {
        var started = clock.GetTimestamp();
        try
        {
            var report = await ReadAllAsync();
            Keep(report);
            var lines = report.Backups!.Periods.Concat(report.Pages!.Periods)
                .Sum(static period => period.Lines.Count);
            var durationMs = ElapsedMs(started);
            ScalewayBillingLog.Read(logger, BillingPeriods.Count, lines, durationMs);
        }
        catch (Exception exception)
        {
            MarkFailed(clock.GetUtcNow());
            var durationMs = ElapsedMs(started);
            ScalewayBillingLog.Unavailable(logger, durationMs, exception);
        }
        finally
        {
            lock (_sync)
            {
                _reading = null;
            }
        }
    }

    private async Task<ScalewayBillReport> ReadAllAsync()
    {
        var fetchedAt = clock.GetUtcNow();
        using var deadline = new CancellationTokenSource(Deadline, clock);
        var periods = await Task.WhenAll(
            BillingPeriods.EndingAt(fetchedAt)
                .Select(period => client.ReadPeriodAsync(period, deadline.Token)));
        return ScalewayBillAssembly.Of(periods, options.Value, fetchedAt);
    }

    private void Keep(ScalewayBillReport good)
    {
        lock (_sync)
        {
            _good = good;
            _failedAt = null;
        }
    }

    // The last good bill stays, flagged stale until a reading succeeds.
    private void MarkFailed(DateTimeOffset failedAt)
    {
        lock (_sync)
        {
            _failedAt = failedAt;
        }
    }

    private long ElapsedMs(long started) =>
        (long)clock.GetElapsedTime(started).TotalMilliseconds;
}
