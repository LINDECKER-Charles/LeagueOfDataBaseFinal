using LoDb.Infrastructure.Analytics.Aggregation;

namespace LoDb.Infrastructure.Analytics.Reports.Breakdowns;

/// <summary>
/// The breakdowns the admin explores key by key, past the tops of the report: every page,
/// every entity, every referring host of a period.
/// </summary>
public static class AnalyticsDimensions
{
    /// <summary>The paths viewed.</summary>
    public const string Pages = "pages";

    /// <summary>The entities viewed, keyed <c>{type}:{key}</c>.</summary>
    public const string Entities = "entities";

    /// <summary>The hosts that referred the views, the site itself included.</summary>
    public const string Referers = "referers";

    public static IReadOnlyList<string> All { get; } = [Pages, Entities, Referers];

    /// <summary>The counter map of a daily aggregate that holds the dimension.</summary>
    internal static string BucketOf(string dimension) => dimension switch
    {
        Pages => DailyBuckets.Pages,
        Entities => DailyBuckets.Entities,
        Referers => DailyBuckets.RefHost,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, null),
    };
}
