namespace LoDb.Api.Hosting.SecurityReports;

/// <summary>
/// The outcome of reading a report body: its violations, or why none could be read.
/// </summary>
internal sealed record CspReportRead
{
    private CspReportRead(IReadOnlyList<CspViolation> violations, CspReportRefusal? refusal)
    {
        Violations = violations;
        Refusal = refusal;
    }

    /// <summary>The violations the body reports; empty when it was refused.</summary>
    public IReadOnlyList<CspViolation> Violations { get; }

    /// <summary>Why the body was refused, or null when it was read.</summary>
    public CspReportRefusal? Refusal { get; }

    public static CspReportRead Of(IReadOnlyList<CspViolation> violations) => new(violations, null);

    public static CspReportRead Refused(CspReportRefusal refusal) => new([], refusal);
}
