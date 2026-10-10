using LoDb.Infrastructure.Analytics;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Tests.Analytics;

/// <summary>The bounds of a day and of the erasure are refused at startup below one.</summary>
public sealed class AnalyticsOptionsValidatorTests
{
    public static TheoryData<string> DailyBounds =>
    [
        nameof(AnalyticsOptions.ClientDataEraseDays),
        nameof(AnalyticsOptions.MaxVisitorsPerDay),
        nameof(AnalyticsOptions.MaxRefererHostsPerDay),
    ];

    [Fact]
    public void DefaultsAreAccepted() =>
        Assert.True(Validate(new AnalyticsOptions()).Succeeded);

    [Theory]
    [MemberData(nameof(DailyBounds))]
    public void BoundBelowOneIsRefused(string bound)
    {
        var options = new AnalyticsOptions
        {
            ClientDataEraseDays = ZeroIfIt(bound, nameof(AnalyticsOptions.ClientDataEraseDays)),
            MaxVisitorsPerDay = ZeroIfIt(bound, nameof(AnalyticsOptions.MaxVisitorsPerDay)),
            MaxRefererHostsPerDay =
                ZeroIfIt(bound, nameof(AnalyticsOptions.MaxRefererHostsPerDay)),
        };

        var result = Validate(options);

        Assert.True(result.Failed);
        Assert.Contains(bound, result.FailureMessage, StringComparison.Ordinal);
    }

    private static ValidateOptionsResult Validate(
        AnalyticsOptions options) =>
        new AnalyticsOptionsValidator().Validate(null, options);

    private static int ZeroIfIt(string bound, string setting) => bound == setting ? 0 : 1;
}
