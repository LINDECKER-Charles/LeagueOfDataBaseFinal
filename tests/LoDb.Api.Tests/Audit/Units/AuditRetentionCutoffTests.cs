using LoDb.Api.Modules.Audit.Retention;

namespace LoDb.Api.Tests.Audit.Units;

/// <summary>
/// The first instant the retention keeps: midnight UTC six calendar months back, the shorter
/// month clamping the day as the legacy <c>P6M</c> did.
/// </summary>
public sealed class AuditRetentionCutoffTests
{
    [Theory]
    [InlineData("2026-09-26T15:30:00+00:00", "2026-03-26T00:00:00+00:00")]
    [InlineData("2026-08-31T23:59:59+00:00", "2026-02-28T00:00:00+00:00")]
    [InlineData("2028-08-30T08:00:00+00:00", "2028-02-29T00:00:00+00:00")]
    [InlineData("2026-09-26T01:00:00+02:00", "2026-03-25T00:00:00+00:00")]
    public void CutoffIsMidnightSixMonthsBack(string now, string expected)
    {
        var cutoff = AuditRetention.CutoffAt(DateTimeOffset.Parse(now, null));

        Assert.Equal(DateTimeOffset.Parse(expected, null), cutoff);
        Assert.Equal(TimeSpan.Zero, cutoff.Offset);
    }
}
