using LoDb.Api.Modules.Accounts.Security;
using LoDb.Api.Modules.Accounts.Security.Admission;
using LoDb.Api.Tests.Hosting.Support;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LoDb.Api.Tests.AccountsSecurity;

/// <summary>
/// The admission to password hashing: one permit at a time, a bounded queue whose surplus is
/// refused at once, a bounded wait, and every refusal and wait counted; a client that leaves
/// is not a refusal. Bounds that would block a thread or never refuse stop the host.
/// </summary>
public sealed class HashingAdmissionTests : IDisposable
{
    private const string RefusedCounter = "lodb.hashing.refused";
    private const string WaitHistogram = "lodb.hashing.wait";
    private const int QueueOfOne = 1;

    private static readonly TimeSpan MaxWait = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);

    private readonly SecurityMeters _meters = new();
    private readonly FakeTimeProvider _clock = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OneRequestHashesAtATimeAndTheNextFollows()
    {
        using var admission = Admission();
        var first = await admission.EnterAsync(Cancellation);

        var second = admission.EnterAsync(Cancellation);
        Assert.False(second.IsCompleted);
        first.Dispose();
        using var granted = await second;

        Assert.True(granted.IsAcquired);
    }

    [Fact]
    public async Task FullQueueIsRefusedAtOnce()
    {
        using var refused = _meters.Collect<long>(RefusedCounter);
        using var admission = Admission();
        using var first = await admission.EnterAsync(Cancellation);
        var queued = admission.EnterAsync(Cancellation);

        var busy = await Assert.ThrowsAsync<HashingBusyException>(
            () => admission.EnterAsync(Cancellation));

        Assert.Equal(RetryAfter, busy.RetryAfter);
        Assert.False(queued.IsCompleted);
        var refusal = Assert.Single(refused.GetMeasurementSnapshot());
        Assert.Equal(SecurityMetrics.QueueFull, refusal.Tags[SecurityMetrics.ReasonTag]);
        first.Dispose();
        (await queued).Dispose();
    }

    [Fact]
    public async Task WaitBeyondItsBoundIsRefused()
    {
        using var refused = _meters.Collect<long>(RefusedCounter);
        using var waits = _meters.Collect<double>(WaitHistogram);
        using var admission = Admission();
        using var first = await admission.EnterAsync(Cancellation);
        var queued = admission.EnterAsync(Cancellation);

        _clock.Advance(MaxWait);

        await Assert.ThrowsAsync<HashingBusyException>(() => queued);
        var refusal = Assert.Single(refused.GetMeasurementSnapshot());
        Assert.Equal(SecurityMetrics.WaitTimeout, refusal.Tags[SecurityMetrics.ReasonTag]);
        Assert.Equal(MaxWait.TotalSeconds, waits.LastMeasurement?.Value);
    }

    [Fact]
    public async Task ClientThatLeavesIsNotRefused()
    {
        using var refused = _meters.Collect<long>(RefusedCounter);
        using var admission = Admission();
        using var first = await admission.EnterAsync(Cancellation);
        using var leaving = new CancellationTokenSource();
        var queued = admission.EnterAsync(leaving.Token);

        await leaving.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.Empty(refused.GetMeasurementSnapshot());
    }

    // One row per check: a range, and the delays no range can state.
    [Theory]
    [InlineData("Permits", "3")]
    [InlineData("MaxWait", "00:00:00")]
    public void BoundsOutOfRangeStopTheHost(string key, string value) =>
        StartupValidation.RefusalOf<HashingAdmissionOptions>(
            static builder => builder.Services.AddAccountsProtection(builder.Configuration),
            new Dictionary<string, string?>
            {
                [$"{HashingAdmissionOptions.SectionName}:{key}"] = value,
            });

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    public void BothDelaysMustBePositive(long maxWaitTicks, long retryAfterTicks, bool positive)
    {
        var options = new HashingAdmissionOptions
        {
            MaxWait = TimeSpan.FromTicks(maxWaitTicks),
            RetryAfter = TimeSpan.FromTicks(retryAfterTicks),
        };

        Assert.Equal(positive, options.HasPositiveDelays());
    }

    public void Dispose() => _meters.Dispose();

    private HashingAdmission Admission() => new(
        Options.Create(new HashingAdmissionOptions { QueueLimit = QueueOfOne }),
        _meters.Metrics,
        _clock);
}
