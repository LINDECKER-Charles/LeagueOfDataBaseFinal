namespace LoDb.Desktop.Updates;

/// <summary>How one run of the app looks for updates.</summary>
internal enum UpdateMode
{
    /// <summary>The window: a check at start, then every few hours, in the background.</summary>
    Background,

    /// <summary><c>--smoke</c>: no check, so that the check of a build needs no network.</summary>
    Off,

    /// <summary>
    /// <c>--smoke --apply-updates</c>, for the update E2E: one check at start, awaited with
    /// its download, then the update is handed to Velopack as the process exits.
    /// </summary>
    ApplyAtExit,
}
