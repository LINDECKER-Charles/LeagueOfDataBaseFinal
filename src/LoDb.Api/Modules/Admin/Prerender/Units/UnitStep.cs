using LoDb.Api.Modules.Admin.Prerender.Telemetry;
using LoDb.Api.Modules.Admin.Prerender.Writing;

namespace LoDb.Api.Modules.Admin.Prerender.Units;

/// <summary>
/// Pages of a unit that ended one after the other, and where the unit goes on from: saved
/// together, in one transaction.
/// </summary>
internal sealed record UnitStep
{
    /// <summary>The index of the next page: the unit resumes there after a restart.</summary>
    public required int Cursor { get; init; }

    public int Written { get; init; }

    public int Skipped { get; init; }

    public int Failed { get; init; }

    /// <summary>Bytes written to the store.</summary>
    public long Bytes { get; init; }

    /// <summary>Why the last failed page of the step failed; null when none did.</summary>
    public string? Failure { get; init; }

    /// <summary>
    /// This step and the page at its cursor, which ended as <paramref name="page"/>.
    /// </summary>
    public UnitStep Then(PageResult page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var next = this with { Cursor = Cursor + 1 };
        return page.Outcome switch
        {
            PageOutcome.Written => next with { Written = Written + 1, Bytes = Bytes + page.Bytes },
            PageOutcome.Skipped => next with { Skipped = Skipped + 1 },
            PageOutcome.Failed => next with { Failed = Failed + 1, Failure = page.Reason },
            _ => throw new ArgumentOutOfRangeException(nameof(page), page.Outcome, null),
        };
    }
}
