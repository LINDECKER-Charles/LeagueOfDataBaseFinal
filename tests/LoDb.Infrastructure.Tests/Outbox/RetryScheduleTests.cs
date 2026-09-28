using LoDb.Infrastructure.Outbox;
using LoDb.Infrastructure.Outbox.Delivery;

namespace LoDb.Infrastructure.Tests.Outbox;

/// <summary>
/// The wait doubles after each failure up to its cap, and the last attempt is known.
/// </summary>
public sealed class RetryScheduleTests
{
    private static readonly OutboxOptions Defaults = new();

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(7, 1920)]
    [InlineData(8, 3600)]
    [InlineData(9, 3600)]
    [InlineData(1000, 3600)]
    public void DelayDoublesUpToItsCap(int attempts, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), RetrySchedule.Delay(attempts, Defaults));

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    public void TenthFailureIsTheLast(int attempts, bool last) =>
        Assert.Equal(last, RetrySchedule.IsLast(attempts, Defaults));

    [Fact]
    public void DefaultsGiveUpAboutThreeHoursAfterQueuing()
    {
        var total = Enumerable.Range(1, Defaults.MaxAttempts - 1)
            .Select(static attempt => RetrySchedule.Delay(attempt, Defaults))
            .Aggregate(TimeSpan.Zero, static (sum, delay) => sum + delay);

        Assert.InRange(total, TimeSpan.FromHours(2.5), TimeSpan.FromHours(3.5));
    }
}
