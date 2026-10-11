namespace LoDb.Api.Hosting.SecurityReports;

/// <summary>
/// Counts the reports taken in since the last summary: the summary is one log line a minute,
/// never one per report, so that a broken page or a flood of forged reports stays readable.
/// </summary>
internal sealed class CspReportTally
{
    private readonly Lock _gate = new();
    private Dictionary<CspViolation, int> _violations = [];
    private Dictionary<CspReportRefusal, int> _refusals = [];
    private int _shed;

    public void Record(CspReportRead read)
    {
        ArgumentNullException.ThrowIfNull(read);
        lock (_gate)
        {
            if (read.Refusal is { } refusal)
            {
                _refusals[refusal] = _refusals.GetValueOrDefault(refusal) + 1;
                return;
            }

            foreach (var violation in read.Violations)
            {
                _violations[violation] = _violations.GetValueOrDefault(violation) + 1;
            }
        }
    }

    public void RecordShed()
    {
        lock (_gate)
        {
            _shed++;
        }
    }

    /// <summary>What was counted since the previous call, then starts counting afresh.</summary>
    public CspReportWindow Drain()
    {
        lock (_gate)
        {
            var window = new CspReportWindow
            {
                Violations = _violations,
                Refusals = _refusals,
                Shed = _shed,
            };
            _violations = [];
            _refusals = [];
            _shed = 0;
            return window;
        }
    }
}
