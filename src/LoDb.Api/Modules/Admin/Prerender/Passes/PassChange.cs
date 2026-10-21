namespace LoDb.Api.Modules.Admin.Prerender.Passes;

/// <summary>What became of a request to move a pass to another state.</summary>
internal enum PassChange
{
    /// <summary>The pass is now in the state asked for.</summary>
    Changed,

    /// <summary>No pass has that id.</summary>
    NotFound,

    /// <summary>
    /// The pass cannot go there from where it is (a finished pass, a pass already paused…),
    /// or it moved meanwhile.
    /// </summary>
    Refused,
}
