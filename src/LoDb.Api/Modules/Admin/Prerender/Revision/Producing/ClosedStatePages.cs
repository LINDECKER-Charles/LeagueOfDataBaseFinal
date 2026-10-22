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
/// <para>
/// The version the closing promotion replaced is one of them, though its unit renders it too:
/// the demand queue, a pass, or that unit when the index caught up late, may have stored its
/// page while the index still held the state open, and nothing else would render it again. It
/// costs one page per closed state and locale.
/// </para>
/// <para>
/// The path is the state's, as its opener spells it: the drain renders each version's own
/// spelling, which differs when Data Dragon changed the case of an id within the state.
/// </para>
/// </remarks>
internal static class ClosedStatePages
{
    /// <summary>
    /// The pages of <paramref name="states"/>, version by version, each state's in the site's
    /// locales; past the longest path the queue keeps, none (no page of the catalogue is).
    /// </summary>
    /// <param name="states">The closed states, in any order.</param>
    /// <param name="coverage">The versions the index covers.</param>
    public static IReadOnlyList<RevisionPage> Of(
        IEnumerable<IndexedState> states,
        CoveredVersions coverage)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(coverage);
        return
        [
            .. states
                .Where(static state => state.Last is not null)
                .SelectMany(state => VersionsOf(state, coverage)
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
        CoveredVersions coverage) =>
        coverage.All.Where(version => version >= state.Opener && version <= state.Last!);

    private static RevisionPage PageOf(PatchVersion version, UiLocale locale, string path)
    {
        var code = UiLocales.Code(locale);
        return new RevisionPage($"/{code}/{version.Value}/{path}", version.Value, code);
    }
}
