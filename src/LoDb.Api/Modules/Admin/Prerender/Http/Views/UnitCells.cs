namespace LoDb.Api.Modules.Admin.Prerender.Http.Views;

/// <summary>
/// The one-character codes of a cell, a (version, locale), as the admin's matrices draw them:
/// in a pass (<see cref="Progress.PrerenderVersionProgress.Cells"/>) and in the coverage of an
/// epoch (<see cref="Patches.PrerenderPatchRow.Cells"/>). The panel reads the same characters.
/// </summary>
internal static class UnitCells
{
    /// <summary>A pass's unit not started yet.</summary>
    public const char Pending = '.';

    /// <summary>A pass's unit running in a running pass.</summary>
    public const char Running = 'r';

    /// <summary>
    /// A unit left running in a paused pass: the pause moves the pass alone, and its resumption
    /// takes this unit first.
    /// </summary>
    public const char Stopped = 'z';

    /// <summary>Completed without a failed page; in an epoch, every type held.</summary>
    public const char Completed = 'c';

    /// <summary>
    /// Completed with failed pages; in an epoch, every type covered, one with failures.
    /// </summary>
    public const char CompletedWithFailures = 'w';

    /// <summary>Failed for good; the SSR serves its pages.</summary>
    public const char Failed = 'f';

    /// <summary>A unit left running in a pass no longer active, cancelled or completed.</summary>
    public const char Interrupted = 'i';

    /// <summary>The pass has no unit in this locale for this version.</summary>
    public const char NoUnit = '_';

    /// <summary>In an epoch: some pages or some types, not all.</summary>
    public const char Partial = 'p';

    /// <summary>In an epoch: nothing.</summary>
    public const char Absent = '-';
}
