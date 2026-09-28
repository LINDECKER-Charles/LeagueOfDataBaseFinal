using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoDb.Infrastructure.Persistence.Analytics;

namespace LoDb.Infrastructure.Tests.Analytics.Support;

/// <summary>
/// The legacy stack's own answers on a sample, written by <c>Legacy/generate.php</c> with its
/// classes: the events, the day files and the report it builds from them, the visitor ids
/// and the classification of user agents and referrers.
/// </summary>
internal static class LegacySamples
{
    public static readonly string Folder =
        Path.Combine(AppContext.BaseDirectory, "Analytics", "Legacy");

    public static readonly string DailyFolder = Path.Combine(Folder, "daily");

    public static JsonDocument Json(string name) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, name)));

    /// <summary>
    /// The legacy report of the sample, with the flag the legacy admin passed next to it
    /// (<c>geo_available</c>): the report of the rewrite carries both.
    /// </summary>
    public static JsonElement Report(bool geoAvailable)
    {
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(Folder, "report.json")))!;
        report["geoAvailable"] = geoAvailable;
        return JsonSerializer.SerializeToElement(report);
    }

    /// <summary>The sample's views, in the order the legacy stack logged them.</summary>
    public static IReadOnlyList<AnalyticsEvent> Events() =>
        [.. File.ReadLines(Path.Combine(Folder, "events.ndjson"))
            .Where(static line => line.Length > 0)
            .Select(EventOf)];

    public static string? Text(JsonElement element, string name) =>
        element.GetProperty(name).ValueKind == JsonValueKind.Null
            ? null
            : element.GetProperty(name).GetString();

    private static AnalyticsEvent EventOf(string text)
    {
        using var document = JsonDocument.Parse(text);
        return EventOf(document.RootElement);
    }

    private static AnalyticsEvent EventOf(JsonElement line) => new()
    {
        OccurredAt = DateTimeOffset.Parse(Text(line, "at")!, CultureInfo.InvariantCulture),
        Origin = AnalyticsCaptureOrigin.ServedPage,
        Route = Text(line, "route")!,
        Path = Text(line, "path")!,
        Type = Text(line, "type")!,
        Kind = Text(line, "kind")!,
        Entity = Text(line, "entity"),
        Status = line.GetProperty("status").GetInt16(),
        Version = Text(line, "version"),
        Lang = Text(line, "lang"),
        Locale = Text(line, "locale")!,
        Ip = Text(line, "ip"),
        Visitor = Text(line, "visitor")!,
        UserAgent = Text(line, "ua"),
        Browser = Text(line, "browser")!,
        Os = Text(line, "os")!,
        Device = Text(line, "device")!,
        IsBot = line.GetProperty("bot").GetBoolean(),
        RefererHost = Text(line, "refHost"),
        RefererSource = Text(line, "refSource")!,
        Country = Text(line, "country"),
        CountryName = Text(line, "countryName"),
    };
}
