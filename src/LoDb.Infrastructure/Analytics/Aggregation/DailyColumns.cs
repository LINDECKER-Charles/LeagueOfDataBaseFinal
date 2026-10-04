using System.Text;
using System.Text.Json;

namespace LoDb.Infrastructure.Analytics.Aggregation;

/// <summary>
/// A daily aggregate to and from the four JSON columns of <c>analytics_daily</c>, in the
/// field names of the legacy files: <c>totals</c> holds <c>views</c>, <c>botViews</c>,
/// <c>byHour</c> and <c>byWeekday</c>; <c>buckets</c> the fifteen counter maps.
/// </summary>
internal static class DailyColumns
{
    public const string ViewsField = "views";
    public const string BotViewsField = "botViews";
    public const string ByHourField = "byHour";
    public const string ByWeekdayField = "byWeekday";

    public static string TotalsOf(DailyAggregate daily)
    {
        ArgumentNullException.ThrowIfNull(daily);
        return Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber(ViewsField, daily.Views);
            writer.WriteNumber(BotViewsField, daily.BotViews);
            WriteVector(writer, ByHourField, daily.ByHour);
            WriteVector(writer, ByWeekdayField, daily.ByWeekday);
            writer.WriteEndObject();
        });
    }

    public static string BucketsOf(DailyAggregate daily)
    {
        ArgumentNullException.ThrowIfNull(daily);
        return Write(writer =>
        {
            writer.WriteStartObject();
            foreach (var name in DailyBuckets.Names)
            {
                writer.WriteStartObject(name);
                foreach (var (key, count) in daily.Buckets[name])
                {
                    writer.WriteNumber(key, count);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        });
    }

    public static string VisitorsOf(DailyAggregate daily)
    {
        ArgumentNullException.ThrowIfNull(daily);
        return JsonSerializer.Serialize(daily.Visitors);
    }

    public static string CountryNamesOf(DailyAggregate daily)
    {
        ArgumentNullException.ThrowIfNull(daily);
        return JsonSerializer.Serialize(daily.CountryNames);
    }

    /// <summary>The aggregate a row holds; a part missing from it counts as empty.</summary>
    public static DailyAggregate Read(DailyRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var daily = new DailyAggregate(row.Day);
        using (var totals = JsonDocument.Parse(row.Totals))
        {
            ReadTotals(totals.RootElement, daily);
        }

        using (var buckets = JsonDocument.Parse(row.Buckets))
        {
            foreach (var name in DailyBuckets.Names)
            {
                if (buckets.RootElement.TryGetProperty(name, out var map))
                {
                    DailyJson.ReadCounters(map, daily, name);
                }
            }
        }

        using var visitors = JsonDocument.Parse(row.Visitors);
        DailyJson.ReadVisitors(visitors.RootElement, daily);
        using var names = JsonDocument.Parse(row.CountryNames);
        DailyJson.ReadNames(names.RootElement, daily.CountryNames);
        return daily;
    }

    /// <summary>Reads <c>views</c>, <c>botViews</c>, <c>byHour</c> and <c>byWeekday</c>.</summary>
    public static void ReadTotals(JsonElement totals, DailyAggregate daily)
    {
        ArgumentNullException.ThrowIfNull(daily);
        if (totals.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (totals.TryGetProperty(ViewsField, out var views)
            && DailyJson.TryCount(views, out var viewCount))
        {
            daily.Views = viewCount;
        }

        if (totals.TryGetProperty(BotViewsField, out var bots)
            && DailyJson.TryCount(bots, out var botCount))
        {
            daily.BotViews = botCount;
        }

        if (totals.TryGetProperty(ByHourField, out var byHour))
        {
            DailyJson.ReadVector(byHour, daily.ByHour);
        }

        if (totals.TryGetProperty(ByWeekdayField, out var byWeekday))
        {
            DailyJson.ReadVector(byWeekday, daily.ByWeekday);
        }
    }

    private static void WriteVector(Utf8JsonWriter writer, string name, long[] values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteNumberValue(value);
        }

        writer.WriteEndArray();
    }

    private static string Write(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
