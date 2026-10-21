using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Writing;

namespace LoDb.Api.Modules.Admin.Prerender.Units;

/// <summary>What one run of a unit did, for its summary line.</summary>
internal sealed class UnitTally
{
    public int Written { get; private set; }

    public int Skipped { get; private set; }

    public int Failed { get; private set; }

    public long Bytes { get; private set; }

    /// <summary>The SSR's refusals waited out: a sign the pass outpaces its budget.</summary>
    public int Refusals { get; private set; }

    /// <summary>Why the last failed page failed: one example, not one line per page.</summary>
    public string? LastFailure { get; private set; }

    public void Add(PageResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Refusals += result.Refusals;
        Bytes += result.Bytes;
        switch (result.Outcome)
        {
            case PageOutcome.Written:
                Written++;
                break;
            case PageOutcome.Skipped:
                Skipped++;
                break;
            case PageOutcome.Failed:
                Failed++;
                LastFailure = result.Reason;
                break;
        }
    }
}
