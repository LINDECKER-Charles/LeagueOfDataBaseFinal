using LoDb.Api.Hosting.SecurityReports.Reports;

namespace LoDb.Api.Hosting.SecurityReports.Summary;

/// <summary>What the API took in over one summary interval.</summary>
internal sealed record CspReportWindow
{
    /// <summary>Violations per directive and disposition.</summary>
    public required IReadOnlyDictionary<CspViolation, int> Violations { get; init; }

    /// <summary>Bodies refused, per reason.</summary>
    public required IReadOnlyDictionary<CspReportRefusal, int> Refusals { get; init; }

    /// <summary>Bodies turned away unread, past the bound of every client together.</summary>
    public required int Shed { get; init; }

    public bool IsEmpty => Violations.Count == 0 && Refusals.Count == 0 && Shed == 0;
}
