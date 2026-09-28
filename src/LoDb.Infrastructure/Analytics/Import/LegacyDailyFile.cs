using System.Globalization;
using System.Text.Json;
using LoDb.Infrastructure.Analytics.Aggregation;

namespace LoDb.Infrastructure.Analytics.Import;

/// <summary>
/// Reads a legacy day file, <c>{yyyy-MM-dd}.json</c>: the aggregate
/// <c>AnalyticsAggregator::aggregateDay</c> wrote, every field at the top level.
/// </summary>
internal static class LegacyDailyFile
{
    private const string Extension = ".json";
    private const string DayFormat = "yyyy-MM-dd";
    private const string DateField = "date";
    private const string VisitorsField = "visitors";
    private const string CountryNamesField = "countryNames";

    /// <summary>The day files of a directory, oldest first.</summary>
    public static IReadOnlyList<string> In(string directory) =>
        [.. Directory.EnumerateFiles(directory, "*" + Extension).Order(StringComparer.Ordinal)];

    /// <returns>Null when the name is not a day or the content no aggregate of that day.</returns>
    public static async Task<DailyAggregate?> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!TryParseDay(Path.GetFileNameWithoutExtension(path), out var day))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);
            return Read(day, document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DailyAggregate? Read(DateOnly day, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || (root.TryGetProperty(DateField, out var date)
                && (date.ValueKind != JsonValueKind.String
                    || !TryParseDay(date.GetString(), out var stated)
                    || stated != day)))
        {
            return null;
        }

        var daily = new DailyAggregate(day);
        DailyColumns.ReadTotals(root, daily);
        foreach (var name in DailyBuckets.Names)
        {
            if (root.TryGetProperty(name, out var map))
            {
                DailyJson.ReadCounters(map, daily, name);
            }
        }

        if (root.TryGetProperty(VisitorsField, out var visitors))
        {
            DailyJson.ReadVisitors(visitors, daily);
        }

        if (root.TryGetProperty(CountryNamesField, out var names))
        {
            DailyJson.ReadNames(names, daily.CountryNames);
        }

        return daily;
    }

    private static bool TryParseDay(string? text, out DateOnly day) =>
        DateOnly.TryParseExact(
            text,
            DayFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out day);
}
