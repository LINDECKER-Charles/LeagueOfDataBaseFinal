using LoDb.Api.Modules.Admin.Prerender.Http.Views;
using LoDb.Api.Modules.Admin.Prerender.Http.Views.Patches;
using LoDb.Api.Modules.Admin.Prerender.Pages;
using LoDb.Domain.Catalog;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;

namespace LoDb.Api.Modules.Admin.Prerender.Coverage;

/// <summary>
/// What an epoch holds of the older patches, a cell per (version, locale) some unit reached;
/// a (version, locale) out of the map holds nothing (<see cref="UnitCells.Absent"/>).
/// </summary>
internal sealed class CoverageMap(
    IReadOnlyDictionary<(string Version, string Locale), CoverageCell> cells)
{
    /// <summary>An epoch no pass wrote.</summary>
    public static CoverageMap Empty { get; } = new(
        new Dictionary<(string Version, string Locale), CoverageCell>());

    /// <summary>The cell of a version and a locale code; null when it holds nothing.</summary>
    public CoverageCell? CellOf(string version, string locale) =>
        cells.GetValueOrDefault((version, locale));

    /// <summary>
    /// The (version, locale code) that hold every type of <paramref name="types"/> (all four
    /// when null): what a pass over these types leaves out when it skips the complete ones.
    /// </summary>
    public IReadOnlySet<(string Version, string Locale)> HeldFor(IReadOnlyList<ResourceType>? types)
    {
        var wanted = TypeMask.Of(types);
        return cells.Where(cell => TypeMask.Holds(cell.Value.Held, wanted))
            .Select(static cell => cell.Key)
            .ToHashSet();
    }

    /// <summary>How much of <paramref name="pinned"/> it holds, in the site's 21 locales.</summary>
    public PrerenderEpochCoverage Summary(IReadOnlyList<PatchVersion> pinned)
    {
        ArgumentNullException.ThrowIfNull(pinned);
        var locales = UiLocales.All.Select(UiLocales.Code).ToList();
        var tally = new Tally();
        foreach (var version in pinned)
        {
            tally.Add(locales.Select(locale => CellOf(version.Value, locale)), locales.Count);
        }

        return new PrerenderEpochCoverage
        {
            PinnedVersions = pinned.Count,
            ExpectedUnits = pinned.Count * locales.Count,
            CompleteUnits = tally.Complete,
            CleanUnits = tally.Clean,
            FailedUnits = tally.Failed,
            GapUnits = tally.Gaps,
            FailedPages = tally.FailedPages,
            CompleteVersions = tally.CompleteVersions,
            GapVersions = tally.GapVersions,
        };
    }

    // The counts of the summary, version after version.
    private sealed class Tally
    {
        public int Complete { get; private set; }

        public int Clean { get; private set; }

        public int Failed { get; private set; }

        public int Gaps { get; private set; }

        public long FailedPages { get; private set; }

        public int CompleteVersions { get; private set; }

        public int GapVersions { get; private set; }

        public void Add(IEnumerable<CoverageCell?> version, int locales)
        {
            var (complete, gaps) = (0, 0);
            foreach (var cell in version)
            {
                switch (cell?.Code)
                {
                    case UnitCells.Completed or UnitCells.CompletedWithFailures:
                        complete++;
                        Clean += cell.Code == UnitCells.Completed ? 1 : 0;
                        FailedPages += cell.FailedPages;
                        break;
                    case UnitCells.Failed:
                        Failed++;
                        break;
                    default:
                        gaps++;
                        break;
                }
            }

            (Complete, Gaps) = (Complete + complete, Gaps + gaps);
            CompleteVersions += complete == locales ? 1 : 0;
            GapVersions += gaps > 0 ? 1 : 0;
        }
    }
}
