namespace LoDb.Api.Modules.Admin.Prerender.Coverage.Index;

/// <summary>
/// What the index of states expects of one covered version, in each locale.
/// </summary>
/// <param name="Openers">
/// The pages an openers pass renders there: the patch page, when the version has one, and the
/// pages that open a state at the version, open or closed.
/// </param>
internal sealed record IndexedPageCounts(int Openers);
