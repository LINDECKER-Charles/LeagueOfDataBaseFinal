using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LoDb.Infrastructure.Persistence.Analytics;

/// <summary>Stored spelling of the analytics values, shared by the model and the raw SQL.</summary>
public static class AnalyticsColumns
{
    internal static readonly ValueConverter<AnalyticsCaptureOrigin, string> OriginConverter =
        new(origin => ToText(origin), value => ParseOrigin(value));

    internal static readonly ValueConverter<AnalyticsDailySource, string> SourceConverter = new(
        source => ToText(source),
        value => ParseSource(value));

    public static string ToText(AnalyticsCaptureOrigin origin) => origin switch
    {
        AnalyticsCaptureOrigin.ServedPage => "page",
        AnalyticsCaptureOrigin.Navigation => "navigation",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
    };

    public static AnalyticsCaptureOrigin ParseOrigin(string value) => value switch
    {
        "page" => AnalyticsCaptureOrigin.ServedPage,
        "navigation" => AnalyticsCaptureOrigin.Navigation,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public static string ToText(AnalyticsDailySource source) => source switch
    {
        AnalyticsDailySource.Events => "events",
        AnalyticsDailySource.Import => "import",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
    };

    public static AnalyticsDailySource ParseSource(string value) => value switch
    {
        "events" => AnalyticsDailySource.Events,
        "import" => AnalyticsDailySource.Import,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };
}
