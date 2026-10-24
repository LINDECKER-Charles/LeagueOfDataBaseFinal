namespace LoDb.Infrastructure.Robots.Reports.Folding;

/// <summary>The shares and means of the reports, zero over nothing.</summary>
internal static class Shares
{
    private const double Percent = 100;

    /// <summary>
    /// The share of <paramref name="count"/> in <paramref name="total"/>, in percent.
    /// </summary>
    public static double Of(long count, double total) => total > 0 ? count / total * Percent : 0;

    public static double Mean(long sum, long count) => count > 0 ? (double)sum / count : 0;
}
