using System.Text.Json;

namespace LoDb.Api.Hosting.SecurityReports.Reports;

/// <summary>
/// Reads the violations of a report body, in the formats browsers send: the Reporting API's
/// array of reports (<c>report-to</c>), one legacy <c>{"csp-report": …}</c> object
/// (<c>report-uri</c>), or one report of the Reporting API's shape on its own, as WebKit posts
/// each violation (under the legacy media type, whichever directive named the endpoint).
/// </summary>
internal static class CspReportFormats
{
    /// <summary>
    /// A browser batches the reports it holds, a hundred at most: anything past that is not a
    /// browser's report and is left unread.
    /// </summary>
    public const int MaxReportsPerBody = 100;

    private const string ReportType = "csp-violation";
    private const string TypeProperty = "type";
    private const string BodyProperty = "body";
    private const string LegacyProperty = "csp-report";
    private const string DispositionProperty = "disposition";
    private const string EffectiveDirective = "effectiveDirective";
    private const string ViolatedDirective = "violatedDirective";
    private const string LegacyEffectiveDirective = "effective-directive";
    private const string LegacyViolatedDirective = "violated-directive";

    /// <returns>The violations, or null when the body is not a report of either format.</returns>
    public static IReadOnlyList<CspViolation>? Parse(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind switch
            {
                JsonValueKind.Array => FromReports(root),
                JsonValueKind.Object => FromObject(root),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // Well-formed JSON whose strings are not Unicode (a byte that is not UTF-8, half a
            // surrogate pair): the parser only finds it when a string is read. Not a browser's
            // report, and never an error of the server.
            return null;
        }
    }

    // Other report types (deprecations, interventions) are not this endpoint's to count.
    private static CspViolation[] FromReports(JsonElement reports) =>
        reports.EnumerateArray()
            .Take(MaxReportsPerBody)
            .Where(IsViolation)
            .Select(FromReport)
            .ToArray();

    private static CspViolation[]? FromObject(JsonElement root) =>
        IsViolation(root) ? [FromReport(root)] : FromLegacy(root);

    private static CspViolation FromReport(JsonElement report) =>
        ViolationOf(report.GetProperty(BodyProperty), EffectiveDirective, ViolatedDirective);

    private static CspViolation[]? FromLegacy(JsonElement root) =>
        root.TryGetProperty(LegacyProperty, out var report)
        && report.ValueKind == JsonValueKind.Object
            ? [ViolationOf(report, LegacyEffectiveDirective, LegacyViolatedDirective)]
            : null;

    private static bool IsViolation(JsonElement report) =>
        report.ValueKind == JsonValueKind.Object
        && StringOf(report, TypeProperty) == ReportType
        && report.TryGetProperty(BodyProperty, out var body)
        && body.ValueKind == JsonValueKind.Object;

    // The effective directive names what was blocked precisely (script-src-elem rather than
    // the default-src that applied); browsers without it only give the violated one.
    private static CspViolation ViolationOf(JsonElement report, string effective, string violated)
    {
        var directive = StringOf(report, effective) ?? StringOf(report, violated);
        return new CspViolation(
            CspDirectives.Of(directive),
            CspDispositions.Of(StringOf(report, DispositionProperty)));
    }

    private static string? StringOf(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
