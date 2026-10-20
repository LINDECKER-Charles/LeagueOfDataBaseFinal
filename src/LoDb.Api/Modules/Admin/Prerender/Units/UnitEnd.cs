namespace LoDb.Api.Modules.Admin.Prerender.Units;

/// <summary>Where the worker left a unit.</summary>
internal enum UnitEnd
{
    /// <summary>Every page ended, or the unit failed for good: on to the next one.</summary>
    Finished,

    /// <summary>The pass was paused or cancelled, or misconfigured: the worker stops.</summary>
    Interrupted,

    /// <summary>
    /// The version's catalog is not stored yet, or not every server holds it for an older
    /// patch yet: the unit stays where it is, and the worker goes on with other versions.
    /// </summary>
    Waiting,
}
