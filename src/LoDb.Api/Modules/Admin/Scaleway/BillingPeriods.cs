using System.Globalization;

namespace LoDb.Api.Modules.Admin.Scaleway;

/// <summary>The billing periods the panels show: calendar months, in UTC.</summary>
internal static class BillingPeriods
{
    /// <summary>The current month and the five before it.</summary>
    public const int Count = 6;

    private const string Format = "yyyy-MM";

    /// <summary>
    /// The <see cref="Count"/> periods ending with the month of <paramref name="now"/>, newest
    /// first, as Scaleway names them (<c>2026-10</c>).
    /// </summary>
    public static IReadOnlyList<string> EndingAt(DateTimeOffset now)
    {
        var utc = now.UtcDateTime;
        var month = new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return
        [
            .. Enumerable.Range(0, Count).Select(back =>
                month.AddMonths(-back).ToString(Format, CultureInfo.InvariantCulture)),
        ];
    }
}
