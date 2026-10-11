namespace LoDb.Api.Hosting.SecurityReports.Reports;

/// <summary>Why a report body was not read.</summary>
internal enum CspReportRefusal
{
    /// <summary>
    /// Neither <c>application/csp-report</c> nor <c>application/reports+json</c>.
    /// </summary>
    UnsupportedType,

    /// <summary>Larger than <see cref="SecurityReportsOptions.MaxBodyBytes"/>.</summary>
    Oversized,

    /// <summary>Not a report of either format.</summary>
    Unreadable,
}
