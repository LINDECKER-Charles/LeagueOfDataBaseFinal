using System.Globalization;
using LoDb.Infrastructure.Persistence.Analytics;

namespace LoDb.Infrastructure.Analytics.Aggregation;

/// <summary>
/// Folds a view into its day, as the legacy <c>AnalyticsAggregator::fold</c> does: robots are
/// counted apart and left out of every other count; the hours and weekdays are UTC.
/// </summary>
internal static class DailyFold
{
    private const long One = 1;

    public static void Fold(DailyAggregate daily, AnalyticsEvent view)
    {
        ArgumentNullException.ThrowIfNull(daily);
        ArgumentNullException.ThrowIfNull(view);
        if (view.IsBot)
        {
            daily.BotViews++;
            return;
        }

        daily.Views++;
        daily.AddVisitor(view.Visitor);
        FoldContent(daily, view);
        FoldAudience(daily, view);
        FoldTime(daily, view.OccurredAt.UtcDateTime);
    }

    private static void FoldContent(DailyAggregate daily, AnalyticsEvent view)
    {
        daily.Count(DailyBuckets.ByType, view.Type, One);
        daily.Count(DailyBuckets.ByKind, view.Kind, One);
        daily.Count(DailyBuckets.ByRoute, view.Route, One);
        daily.Count(
            DailyBuckets.Status,
            view.Status.ToString(CultureInfo.InvariantCulture),
            One);
        CountPresent(daily, DailyBuckets.Pages, view.Path);
        if (!string.IsNullOrEmpty(view.Entity))
        {
            daily.Count(
                DailyBuckets.Entities,
                view.Type + DailyBuckets.EntitySeparator + view.Entity,
                One);
        }
    }

    private static void FoldAudience(DailyAggregate daily, AnalyticsEvent view)
    {
        daily.Count(DailyBuckets.Locale, view.Locale, One);
        daily.Count(DailyBuckets.Browser, view.Browser, One);
        daily.Count(DailyBuckets.Os, view.Os, One);
        daily.Count(DailyBuckets.Device, view.Device, One);
        daily.Count(DailyBuckets.RefSource, view.RefererSource, One);
        CountPresent(daily, DailyBuckets.Lang, view.Lang);
        CountPresent(daily, DailyBuckets.RefHost, view.RefererHost);
        if (!string.IsNullOrEmpty(view.Country))
        {
            daily.Count(DailyBuckets.Country, view.Country, One);
            // The label travels with the code; the day's last one wins, as in the legacy.
            daily.CountryNames[view.Country] = view.CountryName ?? view.Country;
        }
    }

    private static void FoldTime(DailyAggregate daily, DateTime moment)
    {
        var weekday = DailyBuckets.WeekdayOf(moment);
        daily.ByHour[moment.Hour]++;
        daily.ByWeekday[weekday]++;
        daily.Count(DailyBuckets.Heatmap, DailyBuckets.CellOf(weekday, moment.Hour), One);
    }

    private static void CountPresent(DailyAggregate daily, string bucket, string? key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            daily.Count(bucket, key, One);
        }
    }
}
