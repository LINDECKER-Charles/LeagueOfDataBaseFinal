using System.Diagnostics.Metrics;
using LoDb.Api.Hosting.SecurityReports.Reports;
using LoDb.Api.Hosting.Telemetry;

namespace LoDb.Api.Hosting.SecurityReports.Summary;

/// <summary>
/// <c>lodb_csp_reports_total{directive,disposition}</c>: the violations browsers reported. Both
/// labels come from bounded vocabularies (<see cref="CspDirectives"/>,
/// <see cref="CspDispositions"/>), whatever a report says.
/// </summary>
internal sealed class CspReportMetrics(IMeterFactory meterFactory)
{
    public const string CounterName = "lodb.csp.reports";
    private const string DirectiveTag = "directive";
    private const string DispositionTag = "disposition";

    private readonly Counter<long> _reports = meterFactory
        .Create(BuildInfoMetrics.MeterName)
        .CreateCounter<long>(
            CounterName,
            unit: "{report}",
            description: "Content-Security-Policy violations reported by browsers.");

    public void Record(IEnumerable<CspViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);
        foreach (var violation in violations)
        {
            _reports.Add(
                1,
                new KeyValuePair<string, object?>(DirectiveTag, violation.Directive),
                new KeyValuePair<string, object?>(DispositionTag, violation.Disposition));
        }
    }
}
