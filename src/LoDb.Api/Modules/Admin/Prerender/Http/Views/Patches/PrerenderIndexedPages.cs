namespace LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;

/// <summary>What the index of states expects of an older patch, in each locale.</summary>
internal sealed record PrerenderIndexedPages
{
    /// <summary>
    /// The pages an openers pass renders: the patch page, when the patch has one, and the
    /// pages that open a state at it, open or closed.
    /// </summary>
    public required int Openers { get; init; }

    /// <summary>
    /// The entity pages of the closed states it holds: stored <c>archived</c>, they never
    /// change again. Its lists and its patch page are frozen too, and not counted here.
    /// </summary>
    public required int Frozen { get; init; }

    /// <summary>
    /// The entity pages of the open states it holds: stored <c>revisable</c>, each rendered
    /// again once a promotion closes its state.
    /// </summary>
    public required int Revisable { get; init; }
}
