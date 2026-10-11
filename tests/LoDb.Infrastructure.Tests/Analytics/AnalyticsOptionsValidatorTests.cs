using LoDb.Infrastructure.Analytics;
using Microsoft.Extensions.Options;

namespace LoDb.Infrastructure.Tests.Analytics;

/// <summary>
/// The bounds of a day are refused at startup below one, and the erasure window below the
/// two days that can separate two runs of the daily job.
/// </summary>
public sealed class AnalyticsOptionsValidatorTests
{
    /// <summary>Each bound, its highest refused value and its lowest accepted one.</summary>
    public static TheoryData<string, int, int> Bounds => new()
    {
        { nameof(AnalyticsOptions.ClientDataEraseDays), 1, 2 },
        { nameof(AnalyticsOptions.MaxVisitorsPerDay), 0, 1 },
        { nameof(AnalyticsOptions.MaxRefererHostsPerDay), 0, 1 },
    };

    [Fact]
    public void DefaultsAreAccepted() =>
        Assert.True(Validate(new AnalyticsOptions()).Succeeded);

    [Theory]
    [MemberData(nameof(Bounds))]
    public void BoundUnderItsFloorIsRefused(string bound, int refused, int accepted)
    {
        var result = Validate(With(bound, refused));

        Assert.True(result.Failed);
        Assert.Contains(bound, result.FailureMessage, StringComparison.Ordinal);
        Assert.True(Validate(With(bound, accepted)).Succeeded);
    }

    private static ValidateOptionsResult Validate(AnalyticsOptions options) =>
        new AnalyticsOptionsValidator().Validate(null, options);

    // The defaults, but for the one bound under test.
    private static AnalyticsOptions With(string bound, int value)
    {
        var options = new AnalyticsOptions();
        switch (bound)
        {
            case nameof(AnalyticsOptions.ClientDataEraseDays):
                options.ClientDataEraseDays = value;
                break;
            case nameof(AnalyticsOptions.MaxVisitorsPerDay):
                options.MaxVisitorsPerDay = value;
                break;
            default:
                options.MaxRefererHostsPerDay = value;
                break;
        }

        return options;
    }
}
