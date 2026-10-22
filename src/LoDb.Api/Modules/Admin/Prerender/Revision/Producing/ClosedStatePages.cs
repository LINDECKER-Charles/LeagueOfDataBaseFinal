using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Infrastructure.Persistence.Prerender.Demand;
using LoDb.Ingestion.Catalog.States.Snapshots;

namespace LoDb.Api.Modules.Admin.Prerender.Revision.Producing;

/// <summary>
/// The pages a closure of states changes: every version of each closed state, in every locale,
/// rendered while the state was open (the class <c>revisable</c>, a canonical to the short URL)
/// and stored so since.
/// </summary>
/// <remarks>
/// Versions from the one the last promotion replaced on are left out: they were not older
/// patches before it, so no pass and no queue stored them yet, and the unit of that version
/// renders them (its promotion's settling delay waited out). The path is the state's, as its
/// opener spells it: the slug and the id are the same in every version of a state but for
/// the case of a champion id Data Dragon changed, whose page then redirects and is skipped.
/// </remarks>
internal static class ClosedStatePages
{
    /// <summary>
    /// The pages of <paramref name="states"/>, version by version, each state's in the site's
    /// locales; past the longest path the queue keeps, none (no page of the catalogue is).
    /// </summary>
    /// <param name="states">The closed states, in any order.</param>
    /// <param name="coverage">The versions the index covers.</param>
    /// <param name="leaving">
    /// The version the latest one replaced at the last promotion; null when there was none.
    /// </param>
    public static IReadOnlyList<RevisionPage> Of(
        IEnumerable<IndexedState> states,
        CoveredVersions coverage,
        PatchVersion? leaving)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(coverage);
        return
        [
            .. states
                .Where(static state => state.Last is not null)
                .SelectMany(state => VersionsOf(state, coverage, leaving)
                    .Select(version => (Version: version, Path: state.Path)))
                .OrderBy(static page => page.Version)
                .ThenBy(static page => page.Path, StringComparer.Ordinal)
                .SelectMany(static page => UiLocales.All.Select(locale =>
                    PageOf(page.Version, locale, page.Path)))
                .Where(static page => page.Path.Length <= PrerenderDemandPage.PathMaxLength),
        ];
    }

    private static IEnumerable<PatchVersion> VersionsOf(
        IndexedState state,
        CoveredVersions coverage,
        PatchVersion? leaving) =>
        coverage.All.Where(version =>
            version >= state.Opener
            && version <= state.Last!
            && (leaving is null || version < leaving));

    private static RevisionPage PageOf(PatchVersion version, UiLocale locale, string path)
    {
        var code = UiLocales.Code(locale);
        return new RevisionPage($"/{code}/{version.Value}/{path}", version.Value, code);
    }
}
