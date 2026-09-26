using System.Globalization;

namespace LoDb.Infrastructure.Analytics.Aggregation;

/// <summary>
/// The shape of a daily aggregate, the legacy stack's (<c>AnalyticsAggregator</c>): fifteen
/// counter maps, the hours and weekdays of the views, and the heatmap's cell keys.
/// </summary>
internal static class DailyBuckets
{
    public const string ByType = "byType";
    public const string ByKind = "byKind";
    public const string ByRoute = "byRoute";
    public const string Status = "status";
    public const string Pages = "pages";
    public const string Entities = "entities";
    public const string Heatmap = "heatmap";
    public const string Locale = "locale";
    public const string Lang = "lang";
    public const string Browser = "browser";
    public const string Os = "os";
    public const string Device = "device";
    public const string RefSource = "refSource";
    public const string RefHost = "refHost";
    public const string Country = "country";

    public const int HoursPerDay = 24;
    public const int DaysPerWeek = 7;

    /// <summary>Between the type and the key of an entity: <c>champion:Ahri</c>.</summary>
    public const char EntitySeparator = ':';

    private const char CellSeparator = ':';

    /// <summary>Every counter map, in the order of the legacy files.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        ByType, ByKind, ByRoute, Status, Pages, Entities, Heatmap,
        Locale, Lang, Browser, Os, Device, RefSource, RefHost, Country,
    ];

    /// <summary>The heatmap's key of a weekday (0 = Monday) and an hour: <c>0:13</c>.</summary>
    public static string CellOf(int weekday, int hour) =>
        string.Create(CultureInfo.InvariantCulture, $"{weekday}{CellSeparator}{hour}");

    /// <summary>The weekday and the hour of a heatmap key; false for a malformed one.</summary>
    public static bool TryParseCell(string key, out int weekday, out int hour)
    {
        ArgumentNullException.ThrowIfNull(key);
        weekday = hour = 0;
        var separator = key.IndexOf(CellSeparator, StringComparison.Ordinal);
        return separator > 0
            && int.TryParse(key[..separator], CultureInfo.InvariantCulture, out weekday)
            && int.TryParse(key[(separator + 1)..], CultureInfo.InvariantCulture, out hour)
            && weekday is >= 0 and < DaysPerWeek
            && hour is >= 0 and < HoursPerDay;
    }

    /// <summary>Monday first, as PHP's <c>N</c> format minus one.</summary>
    public static int WeekdayOf(DateTime moment) =>
        ((int)moment.DayOfWeek + DaysPerWeek - 1) % DaysPerWeek;
}
