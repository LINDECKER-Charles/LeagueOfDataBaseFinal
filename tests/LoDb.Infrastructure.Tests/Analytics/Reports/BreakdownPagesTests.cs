using LoDb.Infrastructure.Analytics.Reports.Breakdowns;

namespace LoDb.Infrastructure.Tests.Analytics.Reports;

/// <summary>
/// A breakdown is searched whatever the case, the most viewed key first, 50 a page, each
/// share of the whole dimension.
/// </summary>
public sealed class BreakdownPagesTests
{
    private static readonly Dictionary<string, long> Counts = new(StringComparer.Ordinal)
    {
        ["/fr/champions/Ahri"] = 30,
        ["/fr/items"] = 50,
        ["/en/champions/Ahri"] = 20,
    };

    [Fact]
    public void ASearchKeepsTheKeysThatHoldItWhateverTheCase()
    {
        var breakdown = BreakdownPages.Of(Counts, Query("AHRI"), "30d");

        Assert.Equal(
            ["/fr/champions/Ahri", "/en/champions/Ahri"],
            breakdown.Rows.Select(static row => row.Name));
        Assert.Equal((100L, 2, 30d), (breakdown.Total, breakdown.Matched, breakdown.Rows[0].Pct));
        Assert.False(breakdown.HasMore);
    }

    [Fact]
    public void KeysPastAPageWaitForTheNext()
    {
        var many = Enumerable.Range(0, 120).ToDictionary(
            static index => $"/p{index:D3}",
            static index => (long)index,
            StringComparer.Ordinal);

        var second = BreakdownPages.Of(many, Query(null) with { Page = 2 }, "7d");
        var third = BreakdownPages.Of(many, Query(null) with { Page = 3 }, "7d");

        Assert.Equal(BreakdownPages.PageSize, second.Rows.Count);
        Assert.Equal("/p069", second.Rows[0].Name);
        Assert.True(second.HasMore);
        Assert.Equal(20, third.Rows.Count);
        Assert.False(third.HasMore);
    }

    private static AnalyticsBreakdownQuery Query(string? search) => new()
    {
        Dimension = AnalyticsDimensions.Pages,
        Search = search,
    };
}
