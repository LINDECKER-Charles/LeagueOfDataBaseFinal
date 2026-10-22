using LoDb.Api.Modules.Seo;
using LoDb.Api.Modules.Seo.Sitemaps;
using LoDb.Domain.Languages;
using LoDb.Domain.Versions;
using LoDb.Ingestion.Catalog.States.Snapshots;
using LoDb.Testing.States;

namespace LoDb.Api.Tests.Seo;

/// <summary>
/// What the sitemap index and a past version's sitemap list, read in the index of states
/// alone (<see cref="SeoStates"/>): files that list something, newest first, each page dated by
/// the moment it last changed.
/// </summary>
public sealed class SitemapIndexTests
{
    private const string Origin = "https://league-of-data-base.com";

    private static readonly EntityStateSnapshot Index = SeoStates.Snapshot();

    private static readonly LatestPatch Latest = LatestPatch.Of(
        PatchVersion.Parse(SeoStates.Latest),
        SeoStates.LatestPromotedAt,
        Index);

    [Fact]
    public void IndexesEveryLocaleLatestFirstThenEachPastFileThatListsSomething()
    {
        var entries = SitemapLocations.Index(Origin, Latest, Index).ToList();
        var count = UiLocales.All.Count;

        Assert.Equal(4 * count, entries.Count);
        Assert.All(entries.Take(count), static entry =>
            Assert.Equal(SeoStates.LatestPromotedAt, entry.LastModified));
        Assert.Equal($"{Origin}/sitemaps/ar/latest.xml", entries[0].Location);
        Assert.Equal(
            new SitemapEntry($"{Origin}/sitemaps/ar/16.18.1.xml", SeoStates.TrinityClosedAt),
            entries[count]);
        Assert.Equal(
            new SitemapEntry($"{Origin}/sitemaps/ar/8.7.1.xml", TestStates.BuiltAt),
            entries[2 * count]);
        Assert.Equal(
            new SitemapEntry($"{Origin}/sitemaps/ar/7.21.1.xml", TestStates.BuiltAt),
            entries[3 * count]);
        Assert.DoesNotContain(entries, static entry =>
            entry.Location.EndsWith("/7.22.1.xml", StringComparison.Ordinal));
    }

    [Fact]
    public void IndexesNoPastVersionBeforeAnyPromotionNorWithoutAnIndex()
    {
        var unpromoted = new LatestPatch(null, null, HasChanges: false);
        var latest = LatestPatch.Of(
            PatchVersion.Parse(SeoStates.Latest),
            SeoStates.LatestPromotedAt,
            EntityStateSnapshot.Empty);

        Assert.Equal(
            UiLocales.All.Count,
            SitemapLocations.Index(Origin, unpromoted, Index).Count());
        Assert.Equal(
            UiLocales.All.Count,
            SitemapLocations.Index(Origin, latest, EntityStateSnapshot.Empty).Count());
        Assert.False(latest.HasChanges);
    }

    [Fact]
    public void ReadsWhetherTheLatestPatchChangedSomething()
    {
        var start = LatestPatch.Of(PatchVersion.Parse(SeoStates.Start), null, Index);
        var unchanged = LatestPatch.Of(PatchVersion.Parse(SeoStates.Unchanged), null, Index);

        Assert.True(Latest.HasChanges);
        Assert.False(start.HasChanges);
        Assert.False(unchanged.HasChanges);
    }

    [Fact]
    public void LaggingIndexAnnouncesItsEndAsPastAndNoPageOfChangesForTheLatest()
    {
        var lagging = SeoStates.Lagging();
        var latest = LatestPatch.Of(
            PatchVersion.Parse(SeoStates.Latest),
            SeoStates.LatestPromotedAt,
            lagging);
        var count = UiLocales.All.Count;

        var entries = SitemapLocations.Index(Origin, latest, lagging).ToList();
        var shortPrefix = SitePages.Prefix(Origin, UiLocale.De, null);
        var pages = SitemapLocations.Latest(shortPrefix, null, latest)
            .Select(static entry => entry.Location);

        Assert.False(latest.HasChanges);
        Assert.Equal(4 * count, entries.Count);
        Assert.Equal(
            new SitemapEntry($"{Origin}/sitemaps/ar/16.18.1.xml", SeoStates.LatestPromotedAt),
            entries[count]);
        Assert.Equal(
            [new SitemapEntry($"{Origin}/de/16.18.1/changes", SeoStates.LatestPromotedAt)],
            SitemapLocations.Version(
                SitePages.Prefix(Origin, UiLocale.De, PatchVersion.Parse(SeoStates.Previous)),
                PatchVersion.Parse(SeoStates.Previous),
                lagging));
        Assert.DoesNotContain($"{Origin}/de/changes", pages);
    }

    [Fact]
    public void PastVersionDatesItsPatchPageByThePromotionThatMadeItPast()
    {
        var entries = VersionOf(SeoStates.Previous);

        Assert.Equal(
            [
                new SitemapEntry(
                    $"{Origin}/de/16.18.1/changes",
                    SeoStates.LatestPromotedAt),
                new SitemapEntry(
                    $"{Origin}/de/16.18.1/items/3078-trinity-force",
                    SeoStates.TrinityClosedAt),
            ],
            entries);
    }

    [Fact]
    public void RebuiltHistoryIsDatedByTheBuild()
    {
        // 8.7.1 left the latest rank before the generation was built.
        var entries = VersionOf(SeoStates.Archived);

        Assert.Equal(
            [
                new SitemapEntry($"{Origin}/de/8.7.1/changes", TestStates.BuiltAt),
                new SitemapEntry($"{Origin}/de/8.7.1/items/3371-molten-edge", TestStates.BuiltAt),
            ],
            entries);
    }

    [Fact]
    public void OpenStatesAndUnchangedVersionsListNothing()
    {
        Assert.Equal(
            [$"{Origin}/de/7.21.1/items/1001-boots"],
            VersionOf(SeoStates.Start).Select(static entry => entry.Location));
        Assert.Empty(VersionOf(SeoStates.Unchanged));
    }

    private static List<SitemapEntry> VersionOf(string version)
    {
        var patch = PatchVersion.Parse(version);
        var prefix = SitePages.Prefix(Origin, UiLocale.De, patch);
        return [.. SitemapLocations.Version(prefix, patch, Index)];
    }
}
