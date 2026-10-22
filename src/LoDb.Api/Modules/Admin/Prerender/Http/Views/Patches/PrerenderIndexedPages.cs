namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;

/// <summary>What the index of states expects of an older patch, in each locale.</summary>
internal sealed record PrerenderIndexedPages
{
    /// <summary>
    /// The pages an openers pass renders: the patch page, when the patch has one, and the
    /// pages that open a state at it, open or closed.
    /// </summary>
    public required int Openers { get; init; }
}
