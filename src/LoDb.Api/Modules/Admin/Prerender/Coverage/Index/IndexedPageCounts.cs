namespace LoDb.Api.Modules.Admin.Prerender.Coverage.Index;

/// <summary>
/// What the index of states expects of one covered version, in each locale.
/// </summary>
/// <param name="Openers">
/// The pages an openers pass renders there: the patch page, when the version has one, and the
/// pages that open a state at the version, open or closed.
/// </param>
/// <param name="Frozen">
/// The entity pages of the closed states that hold the version: stored <c>archived</c>, they
/// never change again (for the fingerprint scheme).
/// </param>
/// <param name="Revisable">
/// The entity pages of the open states that hold the version: stored <c>revisable</c>, each
/// rendered again once a promotion closes its state.
/// </param>
internal sealed record IndexedPageCounts(int Openers, int Frozen, int Revisable);
